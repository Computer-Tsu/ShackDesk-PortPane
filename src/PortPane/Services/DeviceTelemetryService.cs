using System.Text.Json;
using Serilog;

namespace PortPane.Services;

public interface IDeviceTelemetryService
{
    Task ReportDeviceSnapshotAsync(string trigger);
}

public sealed class DeviceTelemetryService : IDeviceTelemetryService
{
    private readonly IComPortService _comPorts;
    private readonly IAudioService _audio;
    private readonly ITelemetryService _telemetry;

    private string _lastSignature = string.Empty;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;

    public DeviceTelemetryService(
        IComPortService comPorts,
        IAudioService audio,
        ITelemetryService telemetry)
    {
        _comPorts = comPorts;
        _audio = audio;
        _telemetry = telemetry;
    }

    public async Task ReportDeviceSnapshotAsync(string trigger)
    {
        if (!_telemetry.IsEnabled) return;

        try
        {
            var snapshot = BuildSnapshot(trigger);
            string signature = JsonSerializer.Serialize(snapshot.Devices
                .OrderBy(d => d.Type)
                .ThenBy(d => d.Vid)
                .ThenBy(d => d.Pid)
                .ThenBy(d => d.DetectionMethod));

            if (signature == _lastSignature && DateTimeOffset.UtcNow - _lastSentAt < TimeSpan.FromMinutes(5))
            {
                Log.Debug("Device telemetry snapshot skipped; unchanged since last report");
                return;
            }

            _lastSignature = signature;
            _lastSentAt = DateTimeOffset.UtcNow;
            await _telemetry.ReportDeviceSnapshotAsync(snapshot);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Device telemetry snapshot failed");
        }
    }

    private DeviceTelemetrySnapshot BuildSnapshot(string trigger)
    {
        var devices = new List<DeviceTelemetryEntry>();
        var comPorts = _comPorts.GetComPorts().Where(p => !p.IsGhost).ToList();
        var audioEndpoints = _audio.GetAllDevices().Where(d => d.IsUsb).ToList();

        devices.AddRange(comPorts.Select(p => new DeviceTelemetryEntry(
            Type: "com",
            Vid: p.Vid,
            Pid: p.Pid,
            UsbDatabaseMatched: p.UsbDatabaseMatched,
            IsRadioInterface: p.IsRadioInterface,
            DetectionMethod: p.DetectionMethod)));

        devices.AddRange(audioEndpoints.Select(d => new DeviceTelemetryEntry(
            Type: d.Flow.ToString().Equals("Render", StringComparison.OrdinalIgnoreCase) ? "audio_render" : "audio_capture",
            Vid: d.Vid,
            Pid: d.Pid,
            UsbDatabaseMatched: d.UsbDatabaseMatched,
            IsRadioInterface: d.IsRadioInterface,
            DetectionMethod: d.DetectionMethod)));

        return new DeviceTelemetrySnapshot(
            Trigger: trigger,
            ComPortCount: comPorts.Count,
            UsbAudioEndpointCount: audioEndpoints.Count,
            KnownDeviceCount: devices.Count(d => d.UsbDatabaseMatched),
            UnknownDeviceCount: devices.Count(d => !d.UsbDatabaseMatched),
            Devices: devices);
    }
}
