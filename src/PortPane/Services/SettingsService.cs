using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PortPane.Models;
using Serilog;

namespace PortPane.Services;

public interface ISettingsService
{
    AppSettings Current         { get; }
    bool        IsPortableMode  { get; }
    string      SettingsDirectory { get; }
    void Save();
    void Reset();
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    private const string FileName = "settings.json";

    public AppSettings Current         { get; private set; }
    public bool        IsPortableMode  { get; }
    public string      SettingsDirectory { get; }

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

        Directory.CreateDirectory(SettingsDirectory);
        Current = Load();

        Log.Debug("SettingsService initialized — portable={Portable}, dir={Dir}",
            IsPortableMode, SettingsDirectory);
    }

    private AppSettings Load()
    {
        string path = Path.Combine(SettingsDirectory, FileName);
        if (!File.Exists(path))
        {
            Log.Debug("No settings file found; using defaults");
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null) return new AppSettings();

            Migrate(loaded);
            Log.Debug("Settings loaded (schema v{Version})", loaded.SchemaVersion);
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings file unreadable; applying defaults");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Increment schema version here and add handlers as the app evolves.
    /// </summary>
    private static void Migrate(AppSettings s)
    {
        // Example future migration:
        // if (s.SchemaVersion < 2) { s.NewField = "default"; s.SchemaVersion = 2; }
    }

    public void Save()
    {
        string path = Path.Combine(SettingsDirectory, FileName);
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

    /// <summary>
    /// Serializes settings to JSON with contributor-friendly _comment_N header keys
    /// prepended to the object. The header documents every field, valid values,
    /// and a warning not to edit manually unless necessary.
    /// </summary>
    private static string SerializeWithHeader(AppSettings settings)
    {
        // Serialize settings to a JsonObject so we can prepend comment keys.
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
            ["_comment_8"]  = "  Use the Settings window inside PortPane instead (Edit > Settings).",
            ["_comment_9"]  = "  A JSON syntax error will cause PortPane to reset all settings to",
            ["_comment_10"] = "  defaults on next launch. Back up this file before editing manually.",
            ["_comment_11"] = "  Validate any manual changes at: https://jsonlint.com",
            ["_comment_12"] = "",
            ["_comment_13"] = "  SCHEMA VERSION",
            ["_comment_14"] = "  SchemaVersion tracks the settings format. PortPane migrates older",
            ["_comment_15"] = "  settings files automatically. Do not change this value manually.",
            ["_comment_16"] = "",
            ["_comment_17"] = "  FIELD REFERENCE",
            ["_comment_18"] = "  SchemaVersion       int     Internal version number. Do not change.",
            ["_comment_19"] = "  ScaleFactor         float   UI zoom level. Range: 0.85 to 2.25.",
            ["_comment_20"] = "                              Default: 1.0 (100%). Change via View menu.",
            ["_comment_21"] = "  AlwaysOnTop         bool    Window stays above other apps. Default: true.",
            ["_comment_22"] = "  WindowPosition      object  Last window position {X, Y} in screen pixels.",
            ["_comment_23"] = "  WindowSize          object  Last window size {Width, Height} in pixels.",
            ["_comment_24"] = "  AudioProfile        string  Active audio profile. Values: 'PC' or 'Radio'.",
            ["_comment_25"] = "  PCModePlayback      string  Playback device ID for PC mode (empty = default).",
            ["_comment_26"] = "  PCModeRecording     string  Capture device ID for PC mode (empty = default).",
            ["_comment_27"] = "  RadioModePlayback   string  Playback device ID for Radio mode (empty = default).",
            ["_comment_28"] = "  RadioModeRecording  string  Capture device ID for Radio mode (empty = default).",
            ["_comment_29"] = "  ComPanelVisible     bool    Show or hide the COM port panel. Default: true.",
            ["_comment_30"] = "  PreferredBaudRate   int     Default baud rate for COM ports. Default: 9600.",
            ["_comment_31"] = "                              Common values: 4800, 9600, 19200, 38400, 57600, 115200.",
            ["_comment_32"] = "  Language            string  UI language code (IETF tag, e.g. 'en', 'de', 'fr').",
            ["_comment_33"] = "                              Default: 'en'. Requires matching Strings.{code}.resx.",
            ["_comment_34"] = "  TelemetryEnabled    bool    Anonymous usage reporting opt-in. Default: false.",
            ["_comment_35"] = "                              Change via Settings window, not by editing here.",
            ["_comment_36"] = "  TelemetryLastSent   string  ISO 8601 UTC timestamp of last telemetry send.",
            ["_comment_37"] = "  UpdateCheckLastRun  string  ISO 8601 UTC timestamp of last update check.",
            ["_comment_38"] = "                              PortPane checks at most once per 24 hours.",
            ["_comment_39"] = "  FirstRunComplete    bool    Set to true after first-run dialog is dismissed.",
            ["_comment_40"] = "  LicenseKey          string  Commercial license key (Base64). Leave empty for GPL.",
            ["_comment_41"] = "  PortableMode        bool    Informational. Actual detection uses portable.txt.",
            ["_comment_42"] = "════════════════════════════════════════════════════════════════════",
        };

        // Append all settings fields after the comment block.
        foreach (var (key, value) in settingsNode)
            output[key] = value?.DeepClone();

        return output.ToJsonString(JsonOptions);
    }

    public void Reset()
    {
        Current = new AppSettings();
        Save();
        Log.Information("Settings reset to defaults");
    }
}
