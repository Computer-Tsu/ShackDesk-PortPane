using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

namespace PortPane.Services;

public interface ITelemetryService
{
    bool IsEnabled { get; set; }
    Task ReportEventAsync(string eventName, IReadOnlyDictionary<string, object>? properties = null);
    Task ReportCrashAsync(Exception ex);
    Task ReportDeviceDetectionAsync(IReadOnlyList<DeviceTelemetryEntry> devices);
    IReadOnlyList<PendingReport> GetPendingReports();
    void ClearPendingReports();
}

public sealed record DeviceTelemetryEntry(
    string  Type,
    string? Vid,
    string? Pid,
    string  FriendlyName,
    string  ClassificationResult,
    string  DetectionMethod);

public sealed record PendingReport(string Id, string EventName, DateTimeOffset Timestamp, string JsonPayload);

public sealed class TelemetryService : ITelemetryService
{
    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private static readonly string PendingDir = PendingReportDir();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public bool IsEnabled
    {
        get => _settings.Current.TelemetryEnabled;
        set
        {
            _settings.Current.TelemetryEnabled = value;
            _settings.Save();
        }
    }

    public TelemetryService(HttpClient http, ISettingsService settings)
    {
        _http     = http;
        _settings = settings;
        Directory.CreateDirectory(PendingDir);
    }

    public async Task ReportEventAsync(string eventName,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        if (!IsEnabled) return;
        var payload = BuildPayload(eventName, properties);
        await SendOrQueueAsync(payload);
    }

    public async Task ReportCrashAsync(Exception ex)
    {
        // Log crash locally regardless of telemetry setting.
        // Only transmit if opted in.
        if (!IsEnabled) return;

        var payload = BuildPayload("crash", new Dictionary<string, object>
        {
            ["exception"] = ex.GetType().Name,
            ["message"]   = ex.Message
            // Stack trace intentionally omitted (may contain file paths)
        });
        await SendOrQueueAsync(payload);
    }

    public async Task ReportDeviceDetectionAsync(IReadOnlyList<DeviceTelemetryEntry> devices)
    {
        if (!IsEnabled) return;
        var payload = BuildPayload("device_detection", new Dictionary<string, object>
        {
            ["device_count"] = devices.Count,
            ["devices"]      = devices
        });
        await SendOrQueueAsync(payload);
    }

    public IReadOnlyList<PendingReport> GetPendingReports()
    {
        var reports = new List<PendingReport>();
        foreach (string file in Directory.GetFiles(PendingDir, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                reports.Add(new PendingReport(
                    Id:          Path.GetFileNameWithoutExtension(file),
                    EventName:   root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "",
                    Timestamp:   root.TryGetProperty("timestamp", out var ts)
                                     ? DateTimeOffset.Parse(ts.GetString()!) : DateTimeOffset.MinValue,
                    JsonPayload: json));
            }
            catch { /* skip malformed files */ }
        }
        return reports.OrderBy(r => r.Timestamp).ToList();
    }

    public void ClearPendingReports()
    {
        foreach (string file in Directory.GetFiles(PendingDir, "*.json"))
            try { File.Delete(file); } catch { }
        Log.Debug("Telemetry pending reports cleared");
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private object BuildPayload(string eventName,
        IReadOnlyDictionary<string, object>? properties)
    {
        var props = properties is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(properties);
        props["install_id"] = _settings.Current.InstallId;

        return new
        {
            report_id = Guid.NewGuid().ToString(),
            app       = BrandingInfo.AppName,
            version   = BrandingInfo.FullVersion,
            @event    = eventName,
            os        = Environment.OSVersion.VersionString,
            timestamp = DateTimeOffset.UtcNow,
            props
        };
    }

    private async Task SendOrQueueAsync(object payload)
    {
        try
        {
            string json    = JsonSerializer.Serialize(payload, JsonOpts);
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(BrandingInfo.TelemetryEndpoint, body);
            Log.Debug("Telemetry sent: {Status}", resp.StatusCode);
        }
        catch
        {
            // Queue for later, silently
            QueueReport(payload);
        }
    }

    private void QueueReport(object payload)
    {
        try
        {
            var all = GetPendingReports();
            // Drop oldest if queue exceeds 10
            if (all.Count >= 10)
            {
                string oldest = Path.Combine(PendingDir, all[0].Id + ".json");
                if (File.Exists(oldest)) File.Delete(oldest);
            }

            string id   = Guid.NewGuid().ToString();
            string json = JsonSerializer.Serialize(payload, JsonOpts);
            File.WriteAllText(Path.Combine(PendingDir, $"{id}.json"), json);
            Log.Debug("Telemetry queued for later delivery: {Id}", id);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to queue telemetry report");
        }
    }

    private static string PendingReportDir()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            BrandingInfo.SuiteName, BrandingInfo.AppName, "pending_reports");
}
