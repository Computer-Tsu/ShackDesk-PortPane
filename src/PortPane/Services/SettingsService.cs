using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PortPane.Models;
using Serilog;

namespace PortPane.Services;

public interface ISettingsService
{
    AppSettings Current           { get; }
    bool        IsPortableMode    { get; }
    string      SettingsDirectory { get; }
    string      SettingsFilePath  { get; }
    string      LicenseFilePath   { get; }
    string      LogFolderPath     { get; }
    void Save();
    void DeleteSettingsFile();
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented               = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    private const string FileName = "settings.json";

    public AppSettings Current           { get; private set; }
    public bool        IsPortableMode    { get; }
    public string      SettingsDirectory { get; }
    public string      SettingsFilePath  => Path.Combine(SettingsDirectory, FileName);
    public string      LogFolderPath     { get; }

    public string LicenseFilePath => IsPortableMode
        ? Path.Combine(SettingsDirectory, "license.portpane")
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            BrandingInfo.SuiteName, BrandingInfo.AppName, "license.portpane");

    public SettingsService()
    {
        IsPortableMode = File.Exists(
            Path.Combine(AppContext.BaseDirectory, "portable.txt"));

        SettingsDirectory = IsPortableMode
            ? Path.Combine(AppContext.BaseDirectory, "PortPane-Data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BrandingInfo.SuiteName,
                BrandingInfo.AppName);

        LogFolderPath = IsPortableMode
            ? Path.Combine(AppContext.BaseDirectory, "PortPane-Data", "logs")
            : Path.Combine(SettingsDirectory, "logs");

        Directory.CreateDirectory(SettingsDirectory);
        Current = Load();

        Log.Debug("SettingsService initialized — portable={Portable}, dir={Dir}",
            IsPortableMode, SettingsDirectory);
    }

    private AppSettings Load()
    {
        string path = SettingsFilePath;
        if (!File.Exists(path))
        {
            Log.Debug("No settings file found; using defaults");
            var defaults = new AppSettings();
            if (ChannelInfo.TelemetryOnByDefault)
                defaults.TelemetryEnabled = true;
            return defaults;
        }

        try
        {
            string json   = File.ReadAllText(path);
            var    loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null) return new AppSettings();

            Migrate(loaded);
            EnsureInstallId(loaded);
            Log.Debug("Settings loaded (schema v{Version})", loaded.SchemaVersion);
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings file unreadable; applying defaults");
            return new AppSettings();
        }
    }

    private static void EnsureInstallId(AppSettings s)
    {
        if (Guid.TryParse(s.InstallId, out var id))
        {
            s.InstallId = id.ToString("D");
            return;
        }

        s.InstallId = Guid.NewGuid().ToString("D");
    }

    /// <summary>
    /// Increment schema version here and add handlers as the app evolves.
    /// v1→v2: Migrate flat audio device fields into AudioProfiles array.
    /// </summary>
    private static void Migrate(AppSettings s)
    {
        if (s.SchemaVersion < 2)
        {
            // Migrate flat PCMode/RadioMode fields into the AudioProfiles array.
            var pc    = s.AudioProfiles.FirstOrDefault(p => p.Id == "pc");
            var radio = s.AudioProfiles.FirstOrDefault(p => p.Id == "radio");

            if (pc is null)
            {
                pc = new AudioProfile { Id = "pc", Name = "PC Mode", IsSystem = true };
                s.AudioProfiles.Add(pc);
            }
            if (radio is null)
            {
                radio = new AudioProfile { Id = "radio", Name = "Radio Mode", IsSystem = true };
                s.AudioProfiles.Add(radio);
            }

            if (!string.IsNullOrEmpty(s.PCModePlayback))
                pc.Playback = s.PCModePlayback;
            if (!string.IsNullOrEmpty(s.PCModeRecording))
                pc.Recording = s.PCModeRecording;
            if (!string.IsNullOrEmpty(s.RadioModePlayback))
                radio.Playback = s.RadioModePlayback;
            if (!string.IsNullOrEmpty(s.RadioModeRecording))
                radio.Recording = s.RadioModeRecording;

            // Clear flat fields — they are now null-suppressed on serialization
            s.PCModePlayback    = null;
            s.PCModeRecording   = null;
            s.RadioModePlayback = null;
            s.RadioModeRecording = null;

            // Migrate AudioProfile string ("PC"/"Radio") to ActiveProfileId
            if (s.AudioProfile is not null)
            {
                s.ActiveProfileId = s.AudioProfile.Equals("Radio", StringComparison.OrdinalIgnoreCase)
                    ? "radio" : "pc";
                s.AudioProfile = null;
            }

            s.SchemaVersion = 2;
            Log.Information("Settings migrated from schema v1 to v2");
        }
    }

    public void Save()
    {
        string path = SettingsFilePath;
        try
        {
            File.WriteAllText(path, SerializeWithHeader(Current));
            Log.Debug("Settings saved");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {Path}", path);
        }
    }

    public void DeleteSettingsFile()
    {
        string path = SettingsFilePath;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Log.Information("Settings file deleted: {Path}", path);
            }
            Current = new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete settings file {Path}", path);
        }
    }

    /// <summary>
    /// Serializes settings to JSON with contributor-friendly _comment_N header keys
    /// prepended to the object. The header documents every field, valid values,
    /// and a warning not to edit manually unless necessary.
    /// </summary>
    private static string SerializeWithHeader(AppSettings settings)
    {
        string settingsJson   = JsonSerializer.Serialize(settings, JsonOptions);
        var    settingsNode   = JsonNode.Parse(settingsJson)!.AsObject();

        var output = new JsonObject
        {
            ["_comment_1"]  = "════════════════════════════════════════════════════════════════════",
            ["_comment_2"]  = "  PortPane by ShackDesk — User Settings",
            ["_comment_3"]  = "  Project  : https://github.com/Computer-Tsu/shackdesk-portpane",
            ["_comment_4"]  = "  Author   : Mark McDow (N4TEK) — My Computer Guru LLC",
            ["_comment_5"]  = "════════════════════════════════════════════════════════════════════",
            ["_comment_6"]  = "",
            ["_comment_7"]  = "  WARNING: Do not manually edit this file unless absolutely necessary.",
            ["_comment_8"]  = "  Use the Settings window inside PortPane instead (File > Settings).",
            ["_comment_9"]  = "  A JSON syntax error will cause PortPane to reset all settings to",
            ["_comment_10"] = "  defaults on next launch. Back up this file before editing manually.",
            ["_comment_11"] = "  Validate any manual changes at: https://jsonlint.com",
            ["_comment_12"] = "",
            ["_comment_13"] = "  SCHEMA VERSION",
            ["_comment_14"] = "  SchemaVersion tracks the settings format. PortPane migrates older",
            ["_comment_15"] = "  settings files automatically. Do not change this value manually.",
            ["_comment_16"] = "",
            ["_comment_17"] = "  FIELD REFERENCE",
            ["_comment_18"] = "  SchemaVersion          int     Internal version number. Do not change.",
            ["_comment_19"] = "  ScaleFactor            float   UI zoom level. Valid: 0.85/1.0/1.35/1.75/2.25.",
            ["_comment_20"] = "  AlwaysOnTop            bool    Window stays above other apps. Default: true.",
            ["_comment_21"] = "  AudioProfiles          array   Audio device profiles (PC Mode, Radio Mode).",
            ["_comment_22"] = "  ActiveProfileId        string  Active profile id. Values: 'pc' or 'radio'.",
            ["_comment_23"] = "  ShowAllAudioDevices    bool    Show all audio devices including built-in.",
            ["_comment_24"] = "  AutoSwitchOnConnect    bool    Auto-switch to Radio Mode on USB connect.",
            ["_comment_25"] = "  AutoSwitchOnRemove     bool    Auto-switch to PC Mode on USB remove.",
            ["_comment_26"] = "  ComPanelVisible        bool    Show or hide the COM port panel. Default: true.",
            ["_comment_27"] = "  PreferredBaudRate      int     Default baud rate. Common: 9600, 115200.",
            ["_comment_28"] = "  ShowUnrecognizedPorts  bool    Show unmatched COM ports. Default: true.",
            ["_comment_29"] = "  HighlightNewPorts      bool    Flash new ports green on connect.",
            ["_comment_30"] = "  ShowPuttyButton        bool    Show PuTTY launch button in COM rows.",
            ["_comment_31"] = "  PuttyExePath           string  Full path to putty.exe.",
            ["_comment_32"] = "  Language               string  UI language code (IETF, e.g. 'en', 'de').",
            ["_comment_33"] = "  LaunchAtStartup        bool    Register in Windows startup. Default: false.",
            ["_comment_34"] = "  InstallId              string  Random Support ID for anonymous telemetry grouping.",
            ["_comment_35"] = "  TelemetryEnabled       bool    Anonymous usage reporting opt-in.",
            ["_comment_36"] = "  TelemetryFrequency     string  'Monthly (if new devices)' or 'Never auto-send'.",
            ["_comment_37"] = "  UpdateCheckLastRun     string  ISO 8601 UTC timestamp of last update check.",
            ["_comment_38"] = "  AutoUpdateEnabled      bool    Automatically check for updates.",
            ["_comment_39"] = "  UpdateCheckFrequency   string  'Weekly', 'Monthly', or 'Never'.",
            ["_comment_40"] = "  UpdateChannel          string  'Stable' or 'Beta'.",
            ["_comment_41"] = "  FirstRunComplete       bool    Set to true after first-run dialog.",
            ["_comment_42"] = "  LicenseKey             string  Commercial license key (Base64).",
            ["_comment_43"] = "  PortableMode           bool    Informational. Detection uses portable.txt.",
            ["_comment_44"] = "════════════════════════════════════════════════════════════════════",
        };

        foreach (var (key, value) in settingsNode)
            output[key] = value?.DeepClone();

        return output.ToJsonString(JsonOptions);
    }
}
