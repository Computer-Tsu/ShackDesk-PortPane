using System.Text.Json.Serialization;

namespace PortPane.Models;

/// <summary>
/// Persisted user settings. All fields must have defaults.
/// Missing fields on load silently apply defaults (handled by SettingsService).
/// Increment SchemaVersion and add a migration handler when fields change.
/// </summary>
public sealed class AppSettings
{
    public int    SchemaVersion       { get; set; } = 1;
    public double ScaleFactor         { get; set; } = 1.0;
    public bool   AlwaysOnTop         { get; set; } = true;

    public WindowPosition WindowPosition { get; set; } = new();
    public WindowSize     WindowSize     { get; set; } = new();

    // Audio
    public string AudioProfile        { get; set; } = "PC";
    public string PCModePlayback      { get; set; } = string.Empty;
    public string PCModeRecording     { get; set; } = string.Empty;
    public string RadioModePlayback   { get; set; } = string.Empty;
    public string RadioModeRecording  { get; set; } = string.Empty;

    // COM
    public bool   ComPanelVisible     { get; set; } = true;
    public int    PreferredBaudRate   { get; set; } = 9600;

    // Localization
    public string Language            { get; set; } = "en";

    // Telemetry
    public bool   TelemetryEnabled    { get; set; } = false;
    public string TelemetryLastSent   { get; set; } = string.Empty;

    // Updates
    public string UpdateCheckLastRun  { get; set; } = string.Empty;

    // Lifecycle
    public bool   FirstRunComplete    { get; set; } = false;

    // License
    public string LicenseKey          { get; set; } = string.Empty;

    // Portable mode (informational — actual detection via portable.txt)
    public bool   PortableMode        { get; set; } = false;
}

public sealed class WindowPosition
{
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
}

public sealed class WindowSize
{
    public double Width  { get; set; } = 420;
    public double Height { get; set; } = 320;
}
