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
    public const string Version           = "0.5.1-beta";
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
