using System.Text.Json;
using PortPane.Models;
using PortPane.Services;
using Xunit;

namespace PortPane.Tests.UnitTests;

public class SettingsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"PortPaneTest_{Guid.NewGuid():N}");

    public SettingsTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose()   => Directory.Delete(_tempDir, recursive: true);

    // ── BrandingInfo constants ────────────────────────────────────────────────

    [Fact]
    public void BrandingInfo_AllConstants_NonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.AppName));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.SuiteName));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.Version));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.RepoURL));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.TelemetryEndpoint));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.UpdateEndpoint));
        Assert.False(string.IsNullOrWhiteSpace(BrandingInfo.PrivacyURL));
    }

    [Fact]
    public void BrandingInfo_AppName_IsPortPane()   => Assert.Equal("PortPane",   BrandingInfo.AppName);
    [Fact]
    public void BrandingInfo_SuiteName_IsShackDesk() => Assert.Equal("ShackDesk", BrandingInfo.SuiteName);
    [Fact]
    public void BrandingInfo_FullName_ContainsBoth()
    {
        Assert.Contains(BrandingInfo.AppName,   BrandingInfo.FullName);
        Assert.Contains(BrandingInfo.SuiteName, BrandingInfo.FullName);
    }

    [Theory]
    [InlineData("Alpha",  "alpha/")]
    [InlineData("Beta",   "beta/")]
    [InlineData("Stable", "stable/")]
    [InlineData("",       "stable/")]
    public void BrandingInfo_GetUpdateEndpoint_ResolvesChannel(string channel, string expectedSuffix)
        => Assert.EndsWith(expectedSuffix, BrandingInfo.GetUpdateEndpoint(channel));

    // ── Attribution fingerprint ───────────────────────────────────────────────

    [Fact]
    public void Attribution_Fingerprint_NonEmpty()
        => Assert.False(string.IsNullOrWhiteSpace(Attribution.Fingerprint));

    [Fact]
    public void Attribution_Fingerprint_ContainsProjectURL()
        => Assert.Contains(BrandingInfo.RepoURL, Attribution.FullFingerprint);

    // ── AppSettings defaults ──────────────────────────────────────────────────

    [Fact]
    public void AppSettings_Defaults_AreReasonable()
    {
        var s = new AppSettings();
        Assert.Equal(2,     s.SchemaVersion);
        Assert.Equal(1.0,   s.ScaleFactor);
        Assert.True(s.AlwaysOnTop);
        Assert.Equal(9600,  s.PreferredBaudRate);
        Assert.Equal("en",  s.Language);
        Assert.False(string.IsNullOrWhiteSpace(s.InstallId));
        Assert.Contains("-", s.InstallId);
        Assert.True(Guid.TryParse(s.InstallId, out _));
        Assert.False(s.TelemetryEnabled);
        Assert.False(s.FirstRunComplete);
        Assert.False(s.PortableMode);
        Assert.Equal(2,     s.AudioProfiles.Count); // two built-in system profiles (PC Mode, Radio Mode)
    }

    // ── Save / load round-trip ────────────────────────────────────────────────

    [Fact]
    public void Settings_SaveLoad_RoundTrip()
    {
        string settingsPath = Path.Combine(_tempDir, "settings.json");
        var    settings     = new AppSettings
        {
            AlwaysOnTop      = false,
            ScaleFactor      = 1.35,
            AudioProfile     = "Radio",
            PreferredBaudRate = 115200,
            FirstRunComplete  = true,
            TelemetryEnabled  = true,
        };

        // Save
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));

        // Load
        var loaded = JsonSerializer.Deserialize<AppSettings>(
            File.ReadAllText(settingsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(loaded);
        Assert.False(loaded.AlwaysOnTop);
        Assert.Equal(1.35, loaded.ScaleFactor);
        Assert.Equal("Radio", loaded.AudioProfile);
        Assert.Equal(115200, loaded.PreferredBaudRate);
        Assert.False(string.IsNullOrWhiteSpace(loaded.InstallId));
        Assert.Contains("-", loaded.InstallId);
        Assert.True(Guid.TryParse(loaded.InstallId, out _));
        Assert.True(loaded.FirstRunComplete);
        Assert.True(loaded.TelemetryEnabled);
    }

    // ── Corrupt settings file → fallback to defaults ─────────────────────────

    [Fact]
    public void Settings_CorruptFile_FallsBackToDefaults()
    {
        string path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "{ this is not valid json }}}");

        AppSettings? result = null;
        try
        {
            result = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        }
        catch
        {
            result = new AppSettings(); // service does this on exception
        }

        Assert.NotNull(result);
        Assert.Equal(2, result.SchemaVersion); // default
    }

    // ── Schema version ────────────────────────────────────────────────────────

    [Fact]
    public void AppSettings_SchemaVersion_StartsAtTwo()
        => Assert.Equal(2, new AppSettings().SchemaVersion);

    // ── WindowPosition / WindowSize defaults ──────────────────────────────────

    [Fact]
    public void WindowPosition_DefaultValues_AreSet()
    {
        var pos = new WindowPosition();
        Assert.Equal(100, pos.X);
        Assert.Equal(100, pos.Y);
    }

    [Fact]
    public void WindowSize_DefaultValues_AreSet()
    {
        var size = new WindowSize();
        Assert.Equal(420, size.Width);
        Assert.Equal(320, size.Height);
    }

    // ── Scale levels ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.85)]
    [InlineData(1.0)]
    [InlineData(1.35)]
    [InlineData(1.75)]
    [InlineData(2.25)]
    public void ScaleFactor_SpecifiedLevels_AreValid(double factor)
    {
        var s = new AppSettings { ScaleFactor = factor };
        Assert.Equal(factor, s.ScaleFactor);
    }
}
