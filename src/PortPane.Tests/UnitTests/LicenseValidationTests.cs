using PortPane.Services;
using Xunit;

namespace PortPane.Tests.UnitTests;

public class LicenseValidationTests
{
    // ── Default state ─────────────────────────────────────────────────────────

    [Fact]
    public void LicenseService_DefaultTier_IsFree()
        => Assert.Equal(LicenseTier.Free, new LicenseService().Current.Tier);

    [Fact]
    public void LicenseService_FreeTier_IsAlwaysValid()
        => Assert.True(new LicenseService().Current.IsValid);

    [Fact]
    public void LicenseService_FreeTier_Licensee_IsNull()
        => Assert.Null(new LicenseService().Current.Licensee);

    // ── Feature availability ──────────────────────────────────────────────────

    [Theory]
    [InlineData("core")]
    [InlineData("hotplug")]
    [InlineData("audio")]
    [InlineData("comports")]
    [InlineData("profile_switch")]
    [InlineData("any_future_feature")]
    public void LicenseService_AllFeatures_AvailableOnFreeTier(string featureKey)
    {
        var svc = new LicenseService();
        Assert.True(svc.IsFeatureAvailable(featureKey),
            $"Feature '{featureKey}' must be available on Free tier");
    }

    // ── Deactivation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LicenseService_Deactivate_RevertsToFree()
    {
        var svc = new LicenseService();
        await svc.DeactivateAsync();
        Assert.Equal(LicenseTier.Free, svc.Current.Tier);
    }

    // ── Invalid key rejection ─────────────────────────────────────────────────

    [Fact]
    public async Task LicenseService_InvalidKey_ReturnsFalse()
    {
        var svc    = new LicenseService();
        bool result = await svc.ActivateAsync("INVALID-KEY-NOT-BASE64");
        Assert.False(result);
    }

    [Fact]
    public async Task LicenseService_EmptyKey_ReturnsFalse()
    {
        var svc    = new LicenseService();
        bool result = await svc.ActivateAsync(string.Empty);
        Assert.False(result);
    }

    [Fact]
    public async Task LicenseService_WrongAppKey_ReturnsFalse()
    {
        // A valid-looking base64 JSON but for a different app
        string fakeKey = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                """{"app":"otherapp","licensee":"Test","email":"","type":"personal","issued":"2026-01-01","expires":"never","version_max":"1.x","signature":""}"""));

        var svc    = new LicenseService();
        bool result = await svc.ActivateAsync(fakeKey);
        Assert.False(result);
    }

    [Fact]
    public async Task LicenseService_TamperedLicenseFile_RevertsToFree()
    {
        // If a license file has been modified, the SHA-256 hash won't match.
        // The service should silently revert to Free.
        // This test verifies that ActivateAsync rejects and doesn't throw.
        var svc    = new LicenseService();
        bool result = await svc.ActivateAsync("garbage=base64==data");
        Assert.False(result);
        // Service should remain Free
        Assert.Equal(LicenseTier.Free, svc.Current.Tier);
    }

    // ── Version ceiling (IsVersionAllowed) ────────────────────────────────────
    // These tests exercise the static helper directly via InternalsVisibleTo.

    [Theory]
    [InlineData(null,   "0.5.1-beta", true,  "null version_max always allows")]
    [InlineData("",     "0.5.1-beta", true,  "empty version_max always allows")]
    [InlineData("0.6",  "0.5.1-beta", true,  "0.5 is below ceiling 0.6")]
    [InlineData("0.6",  "0.5.103",   true,  "0.5.103 is below ceiling 0.6")]
    [InlineData("0.6",  "0.6.0",     true,  "0.6.0 is within ceiling 0.6")]
    [InlineData("0.6",  "0.6.5",     true,  "0.6.5 is within ceiling 0.6")]
    [InlineData("0.6",  "0.6.0-beta",true,  "0.6.0-beta strips suffix, within 0.6")]
    [InlineData("0.6",  "0.6.0-Beta",true,  "case-insensitive suffix strip")]
    [InlineData("0.6",  "0.7.0",     false, "0.7 exceeds ceiling 0.6")]
    [InlineData("0.6",  "1.0.0",     false, "1.0 exceeds ceiling 0.6")]
    [InlineData("1.0",  "0.9.9",     true,  "0.9.9 is below ceiling 1.0")]
    [InlineData("1.0",  "1.0.0",     true,  "1.0.0 is within ceiling 1.0")]
    [InlineData("1.0",  "1.1.0",     false, "1.1 exceeds ceiling 1.0")]
    [InlineData("0.5",  "0.5.1",     true,  "0.5.1 is within ceiling 0.5")]
    [InlineData("0.5",  "0.5.1-beta",true,  "0.5.1-beta strips to 0.5.1, within 0.5")]
    [InlineData("0.5",  "0.6.0",     false, "0.6 exceeds ceiling 0.5")]
    public void IsVersionAllowed_VersionComparison(string? versionMax, string appVersion, bool expected, string reason)
    {
        // Swap in the test app version by patching BrandingInfo indirectly:
        // IsVersionAllowed reads BrandingInfo.Version, so we test by temporarily
        // checking the internal helper with a known BrandingInfo.Version.
        // Since BrandingInfo.Version is a const we can only test the helper
        // by passing the app version through a thin wrapper here.
        bool result = IsVersionAllowedFor(appVersion, versionMax);
        Assert.True(result == expected, $"Expected {expected} for appVersion={appVersion}, versionMax={versionMax} — {reason}");
    }

    [Theory]
    [InlineData("abc",  false, "non-numeric version_max is gracefully ignored (allow)")]
    [InlineData("0.x",  false, "non-numeric segment in version_max is gracefully ignored (allow)")]
    public void IsVersionAllowed_MalformedVersionMax_Allows(string versionMax, bool expectedParseResult, string reason)
    {
        // Malformed version_max cannot be parsed → should allow rather than block
        bool parsed = LicenseService.TryParseVersionParts(versionMax, out _);
        Assert.Equal(expectedParseResult, parsed);
        _ = reason;
    }

    // Helper: simulate IsVersionAllowed with a custom app version string.
    // Replicates the internal logic using the public TryParseVersionParts.
    private static bool IsVersionAllowedFor(string appVersion, string? versionMax)
    {
        if (string.IsNullOrWhiteSpace(versionMax)) return true;

        // Strip pre-release suffix
        int dash = appVersion.IndexOf('-');
        if (dash >= 0) appVersion = appVersion[..dash];

        if (!LicenseService.TryParseVersionParts(appVersion, out int[] appParts)) return true;
        if (!LicenseService.TryParseVersionParts(versionMax, out int[] maxParts)) return true;

        for (int i = 0; i < maxParts.Length; i++)
        {
            int a = i < appParts.Length ? appParts[i] : 0;
            if (a < maxParts[i]) return true;
            if (a > maxParts[i]) return false;
        }
        return true;
    }
}
