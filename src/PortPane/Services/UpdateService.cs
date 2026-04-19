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

        try
        {
            var manager = CreateManager();
            var info = await manager.CheckForUpdatesAsync();

            // Record the check time regardless of result
            _settings.Current.UpdateCheckLastRun = DateTimeOffset.UtcNow.ToString("O");
            _settings.Save();

            if (info is null)
            {
                Log.Debug("Update check: already up to date");
                return null;
            }

            string version = info.TargetFullRelease.Version.ToString();
            Log.Information("Update available: {Version}", version);
            return new UpdateAvailable(version, info.TargetFullRelease.NotesMarkdown ?? string.Empty);
        }
        catch (Exception ex)
        {
            // Offline failures must be silent to the user — logged at Debug only.
            Log.Debug(ex, "Update check failed (offline or server unavailable)");
            return null;
        }
    }

    public async Task ApplyUpdateAsync(UpdateAvailable update)
    {
        try
        {
            var manager = CreateManager();
            var info = await manager.CheckForUpdatesAsync();
            if (info is null) return;

            await manager.DownloadUpdatesAsync(info);
            manager.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply update {Version}", update.Version);
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

    private UpdateManager CreateManager()
    {
        string endpoint = BrandingInfo.GetUpdateEndpoint(_settings.Current.UpdateChannel);
        Log.Debug("Update check endpoint: {Endpoint}", endpoint);
        return new UpdateManager(new SimpleWebSource(endpoint));
    }
}
