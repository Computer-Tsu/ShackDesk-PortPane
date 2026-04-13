using Serilog;
using Velopack;
using Velopack.Sources;

namespace PortPane.Services;

public interface IUpdateService
{
    Task<UpdateAvailable?> CheckForUpdateAsync(bool force = false);
    Task ApplyUpdateAsync(UpdateAvailable update);
}

public sealed record UpdateAvailable(string Version, string ReleaseNotes);

public sealed class UpdateService : IUpdateService
{
    private readonly Services.ISettingsService _settings;

    // Cached from the last successful check — reused by ApplyUpdateAsync to
    // avoid a redundant network round-trip between "update found" and "apply".
    private Velopack.UpdateInfo? _lastUpdateInfo;

    public UpdateService(ISettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Checks for updates on the user's selected schedule unless forced.
    /// Runs on caller's thread — ensure this is called from a background thread.
    /// Offline failures are silent (logged at Debug only, per spec).
    /// </summary>
    public async Task<UpdateAvailable?> CheckForUpdateAsync(bool force = false)
    {
        if (!force && !_settings.Current.AutoUpdateEnabled) return null;
        if (!force && !IsCheckDue()) return null;

        string channel  = _settings.Current.UpdateChannel ?? "Stable";
        string endpoint = BrandingInfo.GetUpdateEndpoint(channel);

        Log.Information("Update check started — channel: {Channel}, endpoint: {Endpoint}, forced: {Forced}",
            channel, endpoint, force);

        try
        {
            var manager = CreateManager(endpoint);
            var info = await manager.CheckForUpdatesAsync();

            // Record the check time regardless of result
            _settings.Current.UpdateCheckLastRun = DateTimeOffset.UtcNow.ToString("O");
            _settings.Save();

            if (info is null)
            {
                Log.Information("Update check complete — already up to date (channel: {Channel})", channel);
                _lastUpdateInfo = null;
                return null;
            }

            _lastUpdateInfo = info;
            string version = info.TargetFullRelease.Version.ToString();
            Log.Information("Update found — version: {Version}, channel: {Channel}, notes: {HasNotes}",
                version, channel, !string.IsNullOrEmpty(info.TargetFullRelease.NotesMarkdown));
            return new UpdateAvailable(version, info.TargetFullRelease.NotesMarkdown ?? string.Empty);
        }
        catch (Exception ex)
        {
            // Offline failures must be silent to the user — logged at Information for alpha telemetry.
            Log.Information(ex, "Update check failed — channel: {Channel}, endpoint: {Endpoint}",
                channel, endpoint);
            return null;
        }
    }

    public async Task ApplyUpdateAsync(UpdateAvailable update)
    {
        string channel  = _settings.Current.UpdateChannel ?? "Stable";
        string endpoint = BrandingInfo.GetUpdateEndpoint(channel);

        Log.Information("Update apply started — version: {Version}, channel: {Channel}",
            update.Version, channel);

        try
        {
            var manager = CreateManager(endpoint);

            // Reuse cached UpdateInfo from the preceding check if still valid;
            // fall back to a fresh check if the cache is missing.
            var info = _lastUpdateInfo ?? await manager.CheckForUpdatesAsync();
            if (info is null)
            {
                Log.Information("Update apply cancelled — no update found at apply time (version: {Version})",
                    update.Version);
                return;
            }

            Log.Information("Update download started — version: {Version}", update.Version);
            await manager.DownloadUpdatesAsync(info);
            Log.Information("Update download complete — triggering restart for version: {Version}", update.Version);
            manager.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update apply failed — version: {Version}, channel: {Channel}",
                update.Version, channel);
            throw;
        }
    }

    private bool IsCheckDue()
    {
        TimeSpan interval = (_settings.Current.UpdateCheckFrequency ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "daily"   => TimeSpan.FromDays(1),
            "weekly"  => TimeSpan.FromDays(7),
            "monthly" => TimeSpan.FromDays(30),
            "never"   => TimeSpan.MaxValue,
            _         => TimeSpan.FromDays(30)
        };
        if (interval == TimeSpan.MaxValue) return false;

        string lastRun = _settings.Current.UpdateCheckLastRun;
        if (string.IsNullOrEmpty(lastRun)) return true;

        if (DateTimeOffset.TryParse(lastRun, out var last))
            return DateTimeOffset.UtcNow - last >= interval;

        return true;
    }

    private static UpdateManager CreateManager(string endpoint) =>
        new UpdateManager(new SimpleWebSource(endpoint));
}
