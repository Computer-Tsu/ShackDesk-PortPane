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
    public const string Version           = "0.5.6";

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
    /// <summary>
    /// Whole days remaining before this build expires, or null if no expiry applies.
    /// Returns null when: channel is Stable, BuildExpiryDays is 0, or BuildDate was not stamped by CI.
    /// Returns 0 on the expiry day itself (not negative).
    /// </summary>
    public static int? DaysRemaining
    {
        get
        {
            if (ChannelInfo.BuildExpiryDays <= 0 || string.IsNullOrEmpty(BuildDate))
                return null;
            if (!DateTimeOffset.TryParse(BuildDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var buildDate))
                return null;
            int days = (int)Math.Ceiling((buildDate.AddDays(ChannelInfo.BuildExpiryDays) - DateTimeOffset.UtcNow).TotalDays);
            return Math.Max(0, days);
        }
    }

    public const string AuthorName        = "Mark McDow";
    public const string AuthorCallsign    = "N4TEK";
    public const string AuthorCompany     = "My Computer Guru LLC";
    public const string AuthorGitHub      = "Computer-Tsu";
    public const string RepoURL           = "https://github.com/Computer-Tsu/shackdesk-portpane";
    public const string AppURL            = "https://shackdesk.com";
    public const string SupportURL        = "https://github.com/Computer-Tsu/shackdesk-portpane/discussions";
    public const string DonationURL       = "";
    public const string LicenseType       = "MIT / Commercial";
    public const string TelemetryEndpoint = "https://telemetry.shackdesk.com/report";

    /// <summary>
    /// Per-channel update feed URL. Resolves at runtime based on ChannelInfo.Channel.
    /// Served by ShackDesk-Site as static JSON; updated automatically after each release.
    /// </summary>
    public static string UpdateEndpoint => ChannelInfo.Channel switch
    {
        ReleaseChannel.Alpha  => "https://shackdesk.com/portpane/update/alpha.json",
        ReleaseChannel.Beta   => "https://shackdesk.com/portpane/update/beta.json",
        _                     => "https://shackdesk.com/portpane/update/stable.json"
    };

    public const string PrivacyURL        = "https://shackdesk.com/privacy";
    public const string ContactEmail      = "";
}
