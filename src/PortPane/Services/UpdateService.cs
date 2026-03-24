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
    private readonly UpdateManager _manager;
    private readonly Services.ISettingsService _settings;

    public UpdateService(ISettingsService settings)
    {
        _settings = settings;
        _manager  = new UpdateManager(new SimpleWebSource(BrandingInfo.UpdateEndpoint));
    }

    /// <summary>
    /// Checks for updates at most once per 24 hours unless forced.
    /// Runs on caller's thread — ensure this is called from a background thread.
    /// Offline failures are silent (logged at Debug only, per spec).
    /// </summary>
    public async Task<UpdateAvailable?> CheckForUpdateAsync(bool force = false)
    {
        if (!force && !IsCheckDue()) return null;

        try
        {
            var info = await _manager.CheckForUpdatesAsync();

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
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null) return;

            await _manager.DownloadUpdatesAsync(info);
            _manager.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply update {Version}", update.Version);
            throw;
        }
    }

    private bool IsCheckDue()
    {
        string lastRun = _settings.Current.UpdateCheckLastRun;
        if (string.IsNullOrEmpty(lastRun)) return true;

        if (DateTimeOffset.TryParse(lastRun, out var last))
            return DateTimeOffset.UtcNow - last >= TimeSpan.FromHours(24);

        return true;
    }
}
