namespace PortPane;

public enum ReleaseChannel { Alpha, Beta, Stable }

/// <summary>
/// Release channel constants. This file is intentionally different on each branch
/// (alpha/dev, beta, main) and must never be overwritten by a merge.
/// See .gitattributes — merge=ours ensures this file is always preserved on the
/// target branch when merging code forward from alpha → beta → main.
///
/// To promote code forward:
///   git checkout beta  →  git merge dev   (ChannelInfo.cs stays as beta values)
///   git checkout main  →  git merge beta  (ChannelInfo.cs stays as main values)
/// </summary>
public static class ChannelInfo
{
    /// <summary>Current release channel for this branch.</summary>
    public const ReleaseChannel Channel = ReleaseChannel.Stable;

    /// <summary>
    /// When true, LicenseService returns a Personal-tier license automatically.
    /// Eliminates license friction for alpha testers. Set false on beta and main.
    /// </summary>
    public const bool UnlockAllForTesting = false;

    /// <summary>
    /// Default value for TelemetryEnabled on a fresh install.
    /// Alpha uses opt-out (true) to maximize diagnostic data from known testers.
    /// Beta and main use opt-in (false) per the privacy policy.
    /// </summary>
    public const bool TelemetryOnByDefault = false;

    /// <summary>
    /// When true, Serilog minimum level is Debug. When false, Information.
    /// Alpha builds log everything; beta and main log only informational events.
    /// </summary>
    public const bool VerboseLogging = false;

    /// <summary>
    /// Number of days after BuildDate before the app refuses to start.
    /// 0 = no expiry (used on main). Alpha: 14 days. Beta: 60 days.
    /// Requires BrandingInfo.BuildDate to be stamped by CI at publish time.
    /// </summary>
    public const int BuildExpiryDays = 0;

    /// <summary>
    /// Version suffix appended by CI when composing the full artifact version.
    /// Alpha: "alpha" (CI also appends .yyyyMMdd-HHmm timestamp).
    /// Beta: "beta". Stable: empty string.
    /// </summary>
    public const string VersionSuffix = "";
}
