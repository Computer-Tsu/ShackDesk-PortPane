using System.Text.Json.Serialization;

namespace PortPane.Models;

/// <summary>
/// Persisted user settings. All fields must have defaults.
/// Missing fields on load silently apply defaults (handled by SettingsService).
/// Increment SchemaVersion and add a migration handler when fields change.
/// </summary>
public sealed class AppSettings
{
    public int    SchemaVersion       { get; set; } = 2;
    public double ScaleFactor         { get; set; } = 1.0;
    public bool   AlwaysOnTop         { get; set; } = true;

    public WindowPosition WindowPosition { get; set; } = new();
    public WindowSize     WindowSize     { get; set; } = new();

    // Audio — flat fields retained for migration from schema v1
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AudioProfile        { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PCModePlayback      { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PCModeRecording     { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RadioModePlayback   { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RadioModeRecording  { get; set; }

    // Audio profiles (v2+)
    public List<AudioProfile> AudioProfiles { get; set; } = new()
    {
        new AudioProfile { Id = "pc",    Name = "PC Mode",    Playback = "", Recording = "", IsSystem = true },
        new AudioProfile { Id = "radio", Name = "Radio Mode", Playback = "", Recording = "", IsSystem = true }
    };
    public string ActiveProfileId { get; set; } = "pc";

    // Audio behavior
    public bool   ShowAllAudioDevices  { get; set; } = true;
    public bool   AutoSwitchOnConnect  { get; set; } = false;
    public bool   AutoSwitchOnRemove   { get; set; } = false;

    // COM
    public bool   ComPanelVisible      { get; set; } = true;
    public int    PreferredBaudRate    { get; set; } = 9600;
    public bool   ShowUnrecognizedPorts { get; set; } = true;
    public bool   HighlightNewPorts    { get; set; } = true;

    // PuTTY
    public bool   ShowPuttyButton      { get; set; } = false;
    public string PuttyExePath         { get; set; } = string.Empty;

    // Localization
    public string Language             { get; set; } = "en";

    // Appearance
    public bool   LaunchAtStartup      { get; set; } = false;

    // Telemetry
    public string InstallId            { get; set; } = Guid.NewGuid().ToString("N");
    public bool   TelemetryEnabled     { get; set; } = false;
    public string TelemetryLastSent    { get; set; } = string.Empty;
    public string TelemetryFrequency   { get; set; } = "Monthly";

    // Updates
    public string UpdateCheckLastRun   { get; set; } = string.Empty;
    public bool   AutoUpdateEnabled    { get; set; } = true;
    public string UpdateCheckFrequency { get; set; } = "Monthly";
    public string UpdateChannel        { get; set; } = "Stable";

    // Lifecycle
    public bool   FirstRunComplete     { get; set; } = false;
    public string LastSeenVersion      { get; set; } = string.Empty;

    // License
    public string LicenseKey           { get; set; } = string.Empty;

    // Portable mode (informational — actual detection via portable.txt)
    public bool   PortableMode         { get; set; } = false;
}

public sealed class AudioProfile
{
    public string Id         { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public string Playback   { get; set; } = string.Empty;
    public string Recording  { get; set; } = string.Empty;
    public bool   IsSystem   { get; set; } = false;
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
