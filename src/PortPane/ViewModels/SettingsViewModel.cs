using System.Collections.ObjectModel;
using PortPane.Models;
using PortPane.Services;

namespace PortPane.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IAudioService    _audio;
    private readonly ILicenseService  _license;

    // ── Audio Settings ────────────────────────────────────────────────────────
    public ObservableCollection<string> PlaybackDeviceNames { get; } = [];
    public ObservableCollection<string> CaptureDeviceNames  { get; } = [];

    private string _pcPlayback;
    public string PCPlayback
    {
        get => _pcPlayback;
        set { SetField(ref _pcPlayback, value); _settings.Current.PCModePlayback = value; }
    }

    private string _pcRecording;
    public string PCRecording
    {
        get => _pcRecording;
        set { SetField(ref _pcRecording, value); _settings.Current.PCModeRecording = value; }
    }

    private string _radioPlayback;
    public string RadioPlayback
    {
        get => _radioPlayback;
        set { SetField(ref _radioPlayback, value); _settings.Current.RadioModePlayback = value; }
    }

    private string _radioRecording;
    public string RadioRecording
    {
        get => _radioRecording;
        set { SetField(ref _radioRecording, value); _settings.Current.RadioModeRecording = value; }
    }

    // ── Telemetry ─────────────────────────────────────────────────────────────
    private bool _telemetryEnabled;
    public bool TelemetryEnabled
    {
        get => _telemetryEnabled;
        set { SetField(ref _telemetryEnabled, value); _settings.Current.TelemetryEnabled = value; }
    }

    // ── Appearance ────────────────────────────────────────────────────────────
    public ObservableCollection<ScaleOption> ScaleOptions { get; } =
    [
        new(0.85,  "Small (85%)"),
        new(1.0,   "Normal (100%)"),
        new(1.35,  "Large (135%)"),
        new(1.75,  "Extra Large (175%)"),
        new(2.25,  "Huge (225%)")
    ];

    private ScaleOption _selectedScale;
    public ScaleOption SelectedScale
    {
        get => _selectedScale;
        set { SetField(ref _selectedScale, value); _settings.Current.ScaleFactor = value.Factor; }
    }

    // ── License ───────────────────────────────────────────────────────────────
    public string LicenseTierDisplay => _license.Current.Tier switch
    {
        LicenseTier.Free    => "Free (GPL v3)",
        LicenseTier.Personal => $"Personal — {_license.Current.Licensee}",
        LicenseTier.Club    => $"Club — {_license.Current.Licensee}",
        LicenseTier.EmComm  => $"EMCOMM — {_license.Current.Licensee}",
        _                   => "Unknown"
    };

    private string _licenseKeyInput = string.Empty;
    public string LicenseKeyInput
    {
        get => _licenseKeyInput;
        set => SetField(ref _licenseKeyInput, value);
    }

    private string _licenseStatus = string.Empty;
    public string LicenseStatus
    {
        get => _licenseStatus;
        set => SetField(ref _licenseStatus, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public RelayCommand SaveCommand           { get; }
    public RelayCommand ActivateLicenseCommand { get; }
    public RelayCommand DeactivateLicenseCommand { get; }

    // ── Future stubs (see spec) ───────────────────────────────────────────────
    // "Audio notification on profile switch" — disabled, coming soon
    // "Minimize to tray" — disabled, coming soon

    public bool AudioNotificationEnabled { get; } = false; // TODO: coming soon
    public bool MinimizeToTrayEnabled    { get; } = false; // TODO: coming soon

    public SettingsViewModel(ISettingsService settings, IAudioService audio, ILicenseService license)
    {
        _settings = settings;
        _audio    = audio;
        _license  = license;

        _pcPlayback    = settings.Current.PCModePlayback;
        _pcRecording   = settings.Current.PCModeRecording;
        _radioPlayback = settings.Current.RadioModePlayback;
        _radioRecording = settings.Current.RadioModeRecording;
        _telemetryEnabled = settings.Current.TelemetryEnabled;

        _selectedScale = ScaleOptions.FirstOrDefault(o => Math.Abs(o.Factor - settings.Current.ScaleFactor) < 0.01)
            ?? ScaleOptions[1];

        SaveCommand             = new RelayCommand(Save);
        ActivateLicenseCommand  = new RelayCommand(ActivateLicense);
        DeactivateLicenseCommand = new RelayCommand(DeactivateLicense);

        LoadDeviceNames();
    }

    private void LoadDeviceNames()
    {
        PlaybackDeviceNames.Clear();
        PlaybackDeviceNames.Add(string.Empty);
        foreach (var d in _audio.GetPlaybackDevices())
            PlaybackDeviceNames.Add(d.FriendlyName);

        CaptureDeviceNames.Clear();
        CaptureDeviceNames.Add(string.Empty);
        foreach (var d in _audio.GetCaptureDevices())
            CaptureDeviceNames.Add(d.FriendlyName);
    }

    private void Save()
    {
        _settings.Save();
        LicenseStatus = "Settings saved.";
    }

    private async void ActivateLicense()
    {
        if (string.IsNullOrWhiteSpace(LicenseKeyInput)) return;
        bool ok = await _license.ActivateAsync(LicenseKeyInput.Trim());
        LicenseStatus = ok ? "License activated." : "Invalid or unrecognized license key.";
        OnPropertyChanged(nameof(LicenseTierDisplay));
    }

    private async void DeactivateLicense()
    {
        await _license.DeactivateAsync();
        LicenseStatus = "License removed. Running as Free tier.";
        OnPropertyChanged(nameof(LicenseTierDisplay));
    }
}

public sealed record ScaleOption(double Factor, string Label)
{
    public override string ToString() => Label;
}
