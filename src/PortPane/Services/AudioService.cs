using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using PortPane.Models;
using Serilog;

namespace PortPane.Services;

public interface IAudioService
{
    IReadOnlyList<AudioDeviceInfo> GetAllDevices();
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();
    string? GetDefaultPlaybackId();
    string? GetDefaultCaptureId();
    bool SetDefaultDevice(string deviceId);
}

public sealed record AudioDeviceInfo(
    string   Id,
    string   FriendlyName,
    DataFlow Flow,
    bool     IsDefault,
    bool     IsUsb,
    bool     IsRadioInterface,
    string?  Vid = null,
    string?  Pid = null,
    bool     UsbDatabaseMatched = false,
    string   DetectionMethod = "unknown");

public sealed class AudioService : IAudioService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly UsbDeviceDatabase  _usbDb;

    // USB keyword classification — matches spec requirements
    private static readonly string[] UsbKeywords =
    [
        "usb", "codec", "c-media", "cm108", "cm119", "cm119a",
        "digirig", "signalink", "rigblaster", "usb audio",
        "usb pnp sound device", "usb sound device",
        "microphone (usb)", "speakers (usb)"
    ];

    public AudioService(UsbDeviceDatabase usbDb) => _usbDb = usbDb;

    public IReadOnlyList<AudioDeviceInfo> GetAllDevices()      => GetDevices(DataFlow.All);
    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() => GetDevices(DataFlow.Render);
    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()  => GetDevices(DataFlow.Capture);

    public string? GetDefaultPlaybackId() => TryGetDefault(DataFlow.Render)?.ID;
    public string? GetDefaultCaptureId()  => TryGetDefault(DataFlow.Capture)?.ID;

    private IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        try
        {
            string? defaultPlaybackId = GetDefaultPlaybackId();
            string? defaultCaptureId  = GetDefaultCaptureId();

            return _enumerator
                .EnumerateAudioEndPoints(flow, DeviceState.Active)
                .Select(d =>
                {
                    bool isDefault = d.ID == defaultPlaybackId || d.ID == defaultCaptureId;
                    var classification = Classify(d);

                    Log.Debug("Audio device: {Name} usb={Usb} radio={Radio}", d.FriendlyName,
                        classification.IsUsb, classification.IsRadio);

                    return new AudioDeviceInfo(
                        Id:              d.ID,
                        FriendlyName:    d.FriendlyName,
                        Flow:            d.DataFlow,
                        IsDefault:       isDefault,
                        IsUsb:           classification.IsUsb,
                        IsRadioInterface: classification.IsRadio,
                        Vid:             classification.Vid,
                        Pid:             classification.Pid,
                        UsbDatabaseMatched: classification.DbEntry is not null,
                        DetectionMethod: classification.DetectionMethod);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate audio devices");
            return [];
        }
    }

    /// <summary>
    /// Sets the Windows default audio endpoint for the given device ID.
    /// Uses NAudio's PolicyConfig COM interface — no admin rights required.
    /// </summary>
    public bool SetDefaultDevice(string deviceId)
    {
        try
        {
            PolicyConfigClient.SetDefaultEndpoint(deviceId, Role.Multimedia);
            PolicyConfigClient.SetDefaultEndpoint(deviceId, Role.Communications);
            Log.Information("Default audio device set: {DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set default audio device {DeviceId}", deviceId);
            return false;
        }
    }

    private MMDevice? TryGetDefault(DataFlow flow)
    {
        try { return _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia); }
        catch { return null; }
    }

    private AudioClassification Classify(MMDevice device)
    {
        string propertyText = CollectPropertyText(device.Properties);
        string all = $"{device.FriendlyName} {device.ID} {propertyText}";
        ExtractVidPid(all, out string? vid, out string? pid);

        var dbEntry = _usbDb.Lookup(vid, pid) ?? MatchAudioEntryByName(device.FriendlyName);
        bool isUsb = dbEntry is not null || ClassifyAsUsb(all);
        bool heuristicRadio = IsRadioInterfaceByName(all);
        bool isRadio = dbEntry?.RadioInterface == true || (isUsb && heuristicRadio);
        string detectionMethod = dbEntry is not null
            ? "usb_devices_json"
            : heuristicRadio ? "audio_heuristic" : isUsb ? "usb_audio_heuristic" : "unknown";

        return new AudioClassification(isUsb, isRadio, dbEntry, vid ?? dbEntry?.Vid, pid ?? dbEntry?.Pid, detectionMethod);
    }

    private static bool ClassifyAsUsb(string text)
    {
        string lower = text.ToLowerInvariant();
        return UsbKeywords.Any(k => lower.Contains(k));
    }

    private bool IsRadioInterfaceByName(string text)
    {
        string lower = text.ToLowerInvariant();
        return lower.Contains("signalink")
            || lower.Contains("rigblaster")
            || lower.Contains("digirig")
            || lower.Contains("codec")
            || lower.Contains("cm108")
            || lower.Contains("cm119");
    }

    private UsbDeviceEntry? MatchAudioEntryByName(string name)
    {
        string lower = name.ToLowerInvariant();
        if (!ClassifyAsUsb(lower)) return null;

        return _usbDb.Entries
            .Where(e => e.IsAudio)
            .FirstOrDefault(e =>
            {
                string entryName = e.Name.ToLowerInvariant();
                return lower.Contains(entryName)
                    || (lower.Contains("signalink") && entryName.Contains("signalink"))
                    || (lower.Contains("cm108") && entryName.Contains("cm108"))
                    || (lower.Contains("cm119") && entryName.Contains("cm119"))
                    || (lower.Contains("codec") && entryName.Contains("codec"));
            });
    }

    private static string CollectPropertyText(PropertyStore properties)
    {
        try
        {
            var chunks = new List<string>();
            var type = properties.GetType();
            int count = type.GetProperty("Count")?.GetValue(properties) is int c ? c : 0;
            var indexer = type.GetProperties()
                .FirstOrDefault(p => p.GetIndexParameters().Length == 1
                    && p.GetIndexParameters()[0].ParameterType == typeof(int));

            if (indexer is null) return string.Empty;

            for (int i = 0; i < count; i++)
            {
                object? item = indexer.GetValue(properties, new object[] { i });
                if (item is null) continue;
                chunks.Add(item.ToString() ?? string.Empty);

                foreach (var prop in item.GetType().GetProperties())
                {
                    if (prop.GetIndexParameters().Length > 0) continue;
                    object? value = prop.GetValue(item);
                    if (value is not null) chunks.Add(value.ToString() ?? string.Empty);
                }
            }
            return string.Join(" ", chunks);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void ExtractVidPid(string text, out string? vid, out string? pid)
    {
        vid = pid = null;
        var vidM = System.Text.RegularExpressions.Regex.Match(text, @"VID_([0-9A-Fa-f]{4})");
        var pidM = System.Text.RegularExpressions.Regex.Match(text, @"PID_([0-9A-Fa-f]{4})");
        if (vidM.Success) vid = vidM.Groups[1].Value.ToUpperInvariant();
        if (pidM.Success) pid = pidM.Groups[1].Value.ToUpperInvariant();
    }

    public void Dispose() => _enumerator.Dispose();

    private sealed record AudioClassification(
        bool IsUsb,
        bool IsRadio,
        UsbDeviceEntry? DbEntry,
        string? Vid,
        string? Pid,
        string DetectionMethod);
}

/// <summary>
/// Direct COM interop for IPolicyConfig — the Windows undocumented interface used to set
/// default audio endpoints. This approach is used by SoundSwitch, AudioDeviceCmdlets, and
/// other open-source audio tools. No admin rights required; works on Windows 10/11.
/// </summary>
[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
file interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
    [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
    [PreserveSig] int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
    [PreserveSig] int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
    [PreserveSig] int GetShareMode(string pszDeviceName, IntPtr pMode);
    [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
    [PreserveSig] int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, uint eRole);
    [PreserveSig] int SetEndpointVisibility(string pszDeviceName, bool bVisible);
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
file class PolicyConfigComObject { }

file static class PolicyConfigClient
{
    public static void SetDefaultEndpoint(string deviceId, Role role)
    {
        var config = (IPolicyConfig)new PolicyConfigComObject();
        Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, (uint)role));
    }
}
