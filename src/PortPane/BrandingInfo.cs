namespace PortPane;

/// <summary>
/// Single source of truth for all branding, versioning, and URL constants.
/// Never hardcode any of these values elsewhere in the codebase, UI, installer, or documentation.
/// </summary>
public static class BrandingInfo
{
    public const string AppName           = "PortPane";
    public const string SuiteName         = "ShackDesk";
    public const string FullName          = "PortPane by ShackDesk";
    public const string Version           = "0.5.2";

    /// <summary>
    /// ISO 8601 UTC build timestamp. Empty string in source — patched by CI at
    /// publish time. Used by App.xaml.cs to enforce ChannelInfo.BuildExpiryDays.
    /// </summary>
    public const string BuildDate         = "";

    /// <summary>
    /// Full version string for display, logging, and telemetry.
    /// Composed from Version + ChannelInfo.VersionSuffix at runtime.
    /// Alpha builds also show the BuildDate stamp when available.
    /// </summary>
    public static string FullVersion
    {
        get
        {
            if (string.IsNullOrEmpty(ChannelInfo.VersionSuffix))
                return Version;

            if (ChannelInfo.Channel == ReleaseChannel.Alpha && !string.IsNullOrEmpty(BuildDate)
                && DateTimeOffset.TryParse(BuildDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return $"{Version}-alpha.{dt:yyyyMMdd}";

            return $"{Version}-{ChannelInfo.VersionSuffix}";
        }
    }
    public const string AuthorName        = "Mark McDow";
    public const string AuthorCallsign    = "N4TEK";
    public const string AuthorCompany     = "My Computer Guru LLC";
    public const string AuthorGitHub      = "Computer-Tsu";
    public const string RepoURL           = "https://github.com/Computer-Tsu/shackdesk-portpane";
    public const string AppURL            = "https://shackdesk.app";
    public const string SupportURL        = "https://github.com/Computer-Tsu/shackdesk-portpane/discussions";
    public const string DonationURL       = "";
    public const string LicenseType       = "GPL v3 / Commercial";
    public const string TelemetryEndpoint = "https://telemetry.shackdesk.app/report";
    public const string UpdateEndpoint    = "https://shackdesk.app/update/latest.json";
    public const string PrivacyURL        = "https://shackdesk.app/privacy";
    public const string ContactEmail      = "";
}
