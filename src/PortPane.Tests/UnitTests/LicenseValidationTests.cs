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
                """{"app":"otherap","licensee":"Test","email":"","type":"personal","issued":"2026-01-01","expires":"never","version_max":"1.x","signature":""}"""));

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
}
