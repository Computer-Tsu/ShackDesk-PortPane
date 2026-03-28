using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace PortPane.Models;

/// <summary>
/// In-memory lookup of known USB VID:PID pairs.
/// Loaded from Data/usb_devices.json at startup.
/// Schema: { "version": 1, "last_updated": "...", "devices": [...] }
/// </summary>
public sealed class UsbDeviceDatabase
{
    private readonly Dictionary<string, UsbDeviceEntry> _byVidPid =
        new(StringComparer.OrdinalIgnoreCase);

    public int    Version     { get; private set; }
    public string LastUpdated { get; private set; } = string.Empty;
    public int    Count       => _byVidPid.Count;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // System.Text.Json silently ignores unknown properties when deserializing
        // to a typed class — this includes _comment_N header keys at the top of
        // usb_devices.json. No special configuration is required; the default
        // behavior already handles contributor comment blocks correctly.
    };

    public static UsbDeviceDatabase Load(string jsonPath)
    {
        var db = new UsbDeviceDatabase();
        try
        {
            string json     = File.ReadAllText(jsonPath);
            var    envelope = JsonSerializer.Deserialize<DatabaseEnvelope>(json, JsonOptions);
            if (envelope?.Devices is null) return db;

            db.Version     = envelope.Version;
            db.LastUpdated = envelope.LastUpdated ?? string.Empty;

            foreach (var entry in envelope.Devices)
                db._byVidPid[$"{entry.Vid}:{entry.Pid}"] = entry;

            Log.Debug("USB device database v{Version} loaded: {Count} entries", db.Version, db.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load USB device database from {Path}", jsonPath);
        }
        return db;
    }

    public UsbDeviceEntry? Lookup(string? vid, string? pid)
    {
        if (vid is null || pid is null) return null;
        _byVidPid.TryGetValue($"{vid}:{pid}", out var entry);
        return entry;
    }

    public bool IsRadioInterface(string? vid, string? pid)
        => Lookup(vid, pid)?.RadioInterface == true;

    // Internal envelope matches the JSON top-level object.
    private sealed class DatabaseEnvelope
    {
        [JsonPropertyName("version")]      public int    Version     { get; set; }
        [JsonPropertyName("last_updated")] public string? LastUpdated { get; set; }
        [JsonPropertyName("devices")]      public List<UsbDeviceEntry>? Devices { get; set; }
    }
}

public sealed class UsbDeviceEntry
{
    [JsonPropertyName("vid")]             public string  Vid            { get; set; } = string.Empty;
    [JsonPropertyName("pid")]             public string  Pid            { get; set; } = string.Empty;
    [JsonPropertyName("name")]            public string  Name           { get; set; } = string.Empty;
    [JsonPropertyName("type")]            public string  Type           { get; set; } = string.Empty; // "audio" | "serial"
    [JsonPropertyName("radio_interface")] public bool    RadioInterface { get; set; }
    [JsonPropertyName("baud_hint")]       public int?    BaudHint       { get; set; }
    [JsonPropertyName("flow_control")]    public string? FlowControl    { get; set; }
    [JsonPropertyName("notes")]           public string? Notes          { get; set; }

    public bool IsAudio  => Type.Equals("audio",  StringComparison.OrdinalIgnoreCase);
    public bool IsSerial => Type.Equals("serial", StringComparison.OrdinalIgnoreCase);
}
