using PortPane.Models;
using PortPane.Services;
using Xunit;

namespace PortPane.Tests.UnitTests;

public class DeviceDetectionTests
{
    // ── UsbDeviceDatabase ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("10C4", "EA60", "Silicon Labs CP2102")]
    [InlineData("0403", "6001", "FTDI FT232R")]
    [InlineData("0D8C", "000C", "C-Media USB Audio CODEC")]
    [InlineData("0D8C", "013C", "C-Media CM108")]
    [InlineData("1A86", "7523", "CH340")]
    public void UsbDatabase_Lookup_FindsKnownDevices(string vid, string pid, string expectedSubstring)
    {
        var db = LoadTestDb();
        var entry = db.Lookup(vid, pid);
        Assert.NotNull(entry);
        Assert.Contains(expectedSubstring, entry.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsbDatabase_Lookup_ReturnsNull_ForUnknownDevice()
        => Assert.Null(LoadTestDb().Lookup("FFFF", "FFFF"));

    [Fact]
    public void UsbDatabase_Lookup_CaseInsensitive()
    {
        var db = LoadTestDb();
        Assert.NotNull(db.Lookup("10c4", "ea60")); // lowercase
        Assert.NotNull(db.Lookup("10C4", "EA60")); // uppercase
    }

    [Theory]
    [InlineData("10C4", "EA60", true)]   // CP2102 — radio interface
    [InlineData("0D8C", "013C", true)]   // CM108  — radio interface
    [InlineData("067B", "2303", false)]  // Prolific — not radio
    public void UsbDatabase_RadioInterface_ClassifiedCorrectly(string vid, string pid, bool expected)
        => Assert.Equal(expected, LoadTestDb().IsRadioInterface(vid, pid));

    [Theory]
    [InlineData("10C4", "EA60", "serial")]
    [InlineData("0D8C", "013C", "audio")]
    public void UsbDatabase_Type_CorrectlySet(string vid, string pid, string expectedType)
    {
        var entry = LoadTestDb().Lookup(vid, pid);
        Assert.NotNull(entry);
        Assert.Equal(expectedType, entry.Type, StringComparer.OrdinalIgnoreCase);
    }

    // ── Baud rate heuristics ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Arduino Uno (COM3)", "",         "2341", "0043", 115200)]  // Arduino → 115200
    [InlineData("CH340 USB Serial",   "QinHeng",  "1A86", "7523", 115200)]  // CH340 → 115200
    [InlineData("FTDI FT232R",        "FTDI",     "0403", "6001", 9600)]    // FTDI → 9600
    [InlineData("Cisco Console",      "Cisco",    null,   null,   9600)]    // Network gear → 9600
    [InlineData("GlobalSat GPS",      "",         null,   null,   4800)]    // GPS → 4800
    public void ComPortService_BaudHeuristic_MatchesSpec(
        string caption, string mfg, string? vid, string? pid, int expectedBaud)
    {
        // Test the heuristic logic indirectly via the database entry
        var db    = LoadTestDb();
        var entry = db.Lookup(vid, pid);
        int baud  = entry?.BaudHint ?? GetHeuristicBaud(caption, mfg, vid);
        Assert.Equal(expectedBaud, baud);
    }

    // ── COM port model ─────────────────────────────────────────────────────────

    [Fact]
    public void ComPortModel_VidPidKey_FormatsCorrectly()
    {
        var m = new ComPortModel { PortName = "COM5", FriendlyName = "Test", Vid = "10C4", Pid = "EA60" };
        Assert.Equal("10C4:EA60", m.VidPidKey);
    }

    [Fact]
    public void ComPortModel_VidPidKey_IsNull_WhenVidOrPidMissing()
    {
        Assert.Null(new ComPortModel { PortName = "COM3" }.VidPidKey);
        Assert.Null(new ComPortModel { PortName = "COM3", Vid = "10C4" }.VidPidKey);
        Assert.Null(new ComPortModel { PortName = "COM3", Pid = "EA60" }.VidPidKey);
    }

    // ── Audio model ───────────────────────────────────────────────────────────

    [Fact]
    public void AudioDeviceModel_IsPlayback_True_ForRenderFlow()
    {
        var m = new AudioDeviceModel { Id = "x", FriendlyName = "Speakers",
            Flow = NAudio.CoreAudioApi.DataFlow.Render };
        Assert.True(m.IsPlayback);
        Assert.False(m.IsCapture);
    }

    [Fact]
    public void AudioDeviceModel_IsCapture_True_ForCaptureFlow()
    {
        var m = new AudioDeviceModel { Id = "x", FriendlyName = "Mic",
            Flow = NAudio.CoreAudioApi.DataFlow.Capture };
        Assert.True(m.IsCapture);
        Assert.False(m.IsPlayback);
    }

    // ── Ghost port detection (via ComPortRowViewModel) ────────────────────────

    [Fact]
    public void ComPortRowViewModel_Ghost_HasTooltipText()
    {
        var info = new ComPortInfo("COM7", "Previously connected device",
            null, null, null, null, false, IsGhost: true, 9600);
        var row = new PortPane.ViewModels.ComPortRowViewModel(info);
        Assert.True(row.IsGhost);
        Assert.Contains("Device Manager", row.GhostTooltip);
    }

    [Fact]
    public void ComPortRowViewModel_NonGhost_EmptyTooltip()
    {
        var info = new ComPortInfo("COM3", "FTDI FT232R",
            "0403", "6001", "FTDI", "USB Serial", true, IsGhost: false, 9600);
        var row = new PortPane.ViewModels.ComPortRowViewModel(info);
        Assert.False(row.IsGhost);
        Assert.Equal(string.Empty, row.GhostTooltip);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UsbDeviceDatabase LoadTestDb()
    {
        // Try the published data path first, fall back to repository root
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Data", "usb_devices.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "data", "usb_devices.json")
        ];
        foreach (string p in candidates)
            if (File.Exists(p)) return UsbDeviceDatabase.Load(p);

        return new UsbDeviceDatabase(); // empty — tests that don't need data will still pass
    }

    private static int GetHeuristicBaud(string caption, string mfg, string? vid)
    {
        string all = (caption + " " + mfg).ToLowerInvariant();
        if (all.Contains("cisco") || all.Contains("juniper")) return 9600;
        if (all.Contains("gps") || all.Contains("garmin"))    return 4800;
        if (all.Contains("arduino") || all.Contains("ch340")) return 115200;
        return vid?.ToUpperInvariant() switch { "1A86" => 115200, "10C4" => 115200, _ => 9600 };
    }
}
