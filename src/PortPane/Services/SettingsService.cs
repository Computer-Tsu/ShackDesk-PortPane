using System.Text.Json;
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
            File.WriteAllText(path, JsonSerializer.Serialize(Current, JsonOptions));
            Log.Debug("Settings saved");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {Path}", path);
        }
    }

    public void Reset()
    {
        Current = new AppSettings();
        Save();
        Log.Information("Settings reset to defaults");
    }
}
