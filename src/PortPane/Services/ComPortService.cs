using System.IO.Ports;
using System.Management;
using Microsoft.Win32;
using PortPane.Models;
using Serilog;

namespace PortPane.Services;

public interface IComPortService
{
    IReadOnlyList<ComPortInfo> GetComPorts();
}

public sealed record ComPortInfo(
    string  PortName,
    string  FriendlyName,
    string? Vid,
    string? Pid,
    string? Manufacturer,
    string? Description,
    bool    IsRadioInterface,
    bool    IsGhost,
    int     SuggestedBaudRate);

public sealed class ComPortService : IComPortService
{
    private readonly UsbDeviceDatabase _usbDb;

    // Keyword lists for heuristic classification
    private static readonly string[] RadioKeywords =
        ["ftdi", "cp210x", "cp2102", "ch340", "ch341", "ch342", "prolific", "pl2303",
         "silicon labs", "digirig", "signalink", "usb serial", "rigblaster"];

    private static readonly string[] NetworkGearKeywords =
        ["cisco", "juniper", "console", "management port", "asr", "catalyst", "nexus"];

    private static readonly string[] GpsKeywords =
        ["gps", "garmin", "trimble", "u-blox", "globalsat"];

    private static readonly string[] ArduinoKeywords =
        ["arduino", "ch340", "ch341", "cp210x", "cp2102"];

    public ComPortService(UsbDeviceDatabase usbDb) => _usbDb = usbDb;

    public IReadOnlyList<ComPortInfo> GetComPorts()
    {
        try
        {
            var active  = QueryWmi();
            var allPorts = GetRegistryPorts();
            return MergeWithGhosts(active, allPorts)
                .OrderBy(p => NaturalOrder(p.PortName))
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WMI COM port query failed; falling back to SerialPort.GetPortNames");
            return FallbackPortNames();
        }
    }

    private List<ComPortInfo> QueryWmi()
    {
        var results = new List<ComPortInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

        foreach (ManagementObject obj in searcher.Get())
        {
            string caption  = obj["Caption"]?.ToString()  ?? string.Empty;
            string portName = ExtractPortName(caption);
            if (string.IsNullOrEmpty(portName)) continue;

            string deviceId    = obj["DeviceID"]?.ToString() ?? string.Empty;
            string manufacturer = obj["Manufacturer"]?.ToString() ?? string.Empty;
            string description  = obj["Description"]?.ToString()  ?? string.Empty;

            ExtractVidPid(deviceId, out string? vid, out string? pid);

            var dbEntry    = _usbDb.Lookup(vid, pid);
            bool isRadio   = dbEntry?.RadioInterface == true || IsRadioByKeyword(caption + " " + manufacturer);
            int  baudHint  = dbEntry?.BaudHint ?? SuggestBaudRate(caption, manufacturer, vid, pid);

            Log.Debug("COM port {Port}: {Name} VID={Vid} PID={Pid} radio={Radio} baud={Baud}",
                portName, dbEntry?.Name ?? caption, vid, pid, isRadio, baudHint);

            results.Add(new ComPortInfo(
                PortName:        portName,
                FriendlyName:    dbEntry?.Name ?? caption,
                Vid:             vid,
                Pid:             pid,
                Manufacturer:    manufacturer,
                Description:     description,
                IsRadioInterface: isRadio,
                IsGhost:         false,
                SuggestedBaudRate: baudHint));
        }
        return results;
    }

    /// <summary>
    /// Reads HKLM\HARDWARE\DEVICEMAP\SERIALCOMM for all COM ports, including ghost/phantom ports
    /// that were previously connected but are not currently active.
    /// </summary>
    private static HashSet<string> GetRegistryPorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is null) return ports;
            foreach (string name in key.GetValueNames())
                if (key.GetValue(name) is string port)
                    ports.Add(port);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read registry COM port map");
        }
        return ports;
    }

    private static List<ComPortInfo> MergeWithGhosts(List<ComPortInfo> active, HashSet<string> allPorts)
    {
        var activePorts = active.Select(p => p.PortName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ghosts = allPorts
            .Where(p => !activePorts.Contains(p))
            .Select(p => new ComPortInfo(
                PortName:        p,
                FriendlyName:    "Previously connected device",
                Vid:             null,
                Pid:             null,
                Manufacturer:    null,
                Description:     null,
                IsRadioInterface: false,
                IsGhost:         true,
                SuggestedBaudRate: 9600));

        return [.. active, .. ghosts];
    }

    private List<ComPortInfo> FallbackPortNames()
        => SerialPort.GetPortNames()
            .OrderBy(NaturalOrder)
            .Select(name => new ComPortInfo(name, name, null, null, null, null, false, false, 9600))
            .ToList();

    // ── Heuristics ──────────────────────────────────────────────────────────

    private static bool IsRadioByKeyword(string text)
    {
        string lower = text.ToLowerInvariant();
        return RadioKeywords.Any(k => lower.Contains(k));
    }

    /// <summary>
    /// Suggests a baud rate based on device type heuristics.
    /// Spec-defined rules applied in priority order.
    /// </summary>
    private static int SuggestBaudRate(string caption, string manufacturer, string? vid, string? pid)
    {
        string all = (caption + " " + manufacturer).ToLowerInvariant();

        if (NetworkGearKeywords.Any(k => all.Contains(k)))     return 9600;
        if (GpsKeywords.Any(k => all.Contains(k)))             return 4800;
        if (ArduinoKeywords.Any(k => all.Contains(k)))         return 115200;
        if (all.Contains("digirig") || all.Contains("signalink")) return 9600;
        if (all.Contains("ftdi"))                               return 9600;

        // VID-based fallbacks
        return vid?.ToUpperInvariant() switch
        {
            "1A86" => 115200, // QinHeng (CH340/CH341)
            "10C4" => 115200, // Silicon Labs
            "0403" => 9600,   // FTDI
            _      => 9600
        };
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────

    private static string ExtractPortName(string caption)
    {
        int start = caption.LastIndexOf('(');
        int end   = caption.LastIndexOf(')');
        if (start < 0 || end <= start) return string.Empty;
        string candidate = caption[(start + 1)..end];
        return candidate.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ? candidate : string.Empty;
    }

    private static void ExtractVidPid(string deviceId, out string? vid, out string? pid)
    {
        vid = pid = null;
        var vidM = System.Text.RegularExpressions.Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4})");
        var pidM = System.Text.RegularExpressions.Regex.Match(deviceId, @"PID_([0-9A-Fa-f]{4})");
        if (vidM.Success) vid = vidM.Groups[1].Value.ToUpperInvariant();
        if (pidM.Success) pid = pidM.Groups[1].Value.ToUpperInvariant();
    }

    private static int NaturalOrder(string portName)
    {
        string digits = new(portName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int n) ? n : int.MaxValue;
    }
}
