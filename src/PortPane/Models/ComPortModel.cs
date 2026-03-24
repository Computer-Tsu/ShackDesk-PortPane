namespace PortPane.Models;

/// <summary>
/// Domain model representing a Windows COM port.
/// </summary>
public sealed class ComPortModel
{
    public string PortName     { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string? Vid         { get; init; }
    public string? Pid         { get; init; }
    public string? Manufacturer { get; init; }
    public string? Description { get; init; }

    /// <summary>Lookup key into usb_devices.json for known-device enrichment.</summary>
    public string? VidPidKey => Vid is not null && Pid is not null ? $"{Vid}:{Pid}" : null;
}
