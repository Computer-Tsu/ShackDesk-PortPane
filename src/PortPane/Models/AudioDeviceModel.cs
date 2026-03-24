using NAudio.CoreAudioApi;

namespace PortPane.Models;

/// <summary>
/// Domain model representing a Windows audio endpoint device.
/// </summary>
public sealed class AudioDeviceModel
{
    public string Id          { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public DataFlow Flow      { get; init; }
    public bool IsDefault     { get; init; }
    public bool IsPlayback    => Flow is DataFlow.Render or DataFlow.All;
    public bool IsCapture     => Flow is DataFlow.Capture or DataFlow.All;
}
