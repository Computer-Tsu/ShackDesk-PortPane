using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using PortPane.Models;
using PortPane.Services;

namespace PortPane.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    // ── Injected services ─────────────────────────────────────────────────────
    private readonly ISettingsService  _settings;
    private readonly IAudioService     _audio;
    private readonly ILicenseService   _license;
    private readonly ITelemetryService _telemetry;
    private readonly IUpdateService    _updates;
    private readonly IComPortService   _comPorts;
    private readonly UsbDeviceDatabase _usbDb;
    private readonly MainViewModel     _mainVm;

    // ── Clone / revert state ──────────────────────────────────────────────────
    private readonly double _originalScaleFactor;
    private readonly bool   _originalAlwaysOnTop;

    // ── Pending fields (not yet committed to settings) ────────────────────────
    private double _pendingScaleFactor;
    private bool   _pendingAlwaysOnTop;
    private bool   _pendingShowAllAudioDevices;
    private bool   _pendingAutoSwitchOnConnect;
    private bool   _pendingAutoSwitchOnRemove;
    private bool   _pendingComPanelVisible;
    private bool   _pendingShowPuttyButton;
    private bool   _savedShowPuttyButton;
    private string _pendingPuttyExePath       = string.Empty;
    private bool   _showPuttyPathWarning;
    private int    _pendingPreferredBaudRate;
    private bool   _pendingShowUnrecognizedPorts;
    private bool   _pendingHighlightNewPorts;
    private string _pendingLanguage            = string.Empty;
    private bool   _pendingLaunchAtStartup;
    private bool   _pendingTelemetryEnabled;
    private string _pendingTelemetryFrequency  = string.Empty;
    private bool   _pendingAutoUpdateEnabled;
    private string _pendingUpdateCheckFrequency = string.Empty;
    private string _pendingUpdateChannel        = string.Empty;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler? RequestResetDialog;

    // ── Commands ──────────────────────────────────────────────────────────────
    public RelayCommand OkCommand                 { get; }
    public RelayCommand CancelCommand             { get; }
    public RelayCommand BrowsePuttyCommand        { get; }
    public RelayCommand PlayPcTestCommand         { get; }
    public RelayCommand PlayRadioTestCommand      { get; }
    public RelayCommand OpenUsbDbFolderCommand    { get; }
    public RelayCommand CheckNowCommand           { get; }
    public RelayCommand OpenLogFolderCommand      { get; }
    public RelayCommand OpenSettingsFolderCommand { get; }
    public RelayCommand ShowResetDialogCommand    { get; }
    public RelayCommand BrowseLicenseFileCommand  { get; }
    public RelayCommand GetLicensePersonalCommand { get; }
    public RelayCommand GetLicenseClubCommand     { get; }
    public RelayCommand GetDonateLinkCommand      { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // Constructor
    // ═════════════════════════════════════════════════════════════════════════

    public SettingsViewModel(
        ISettingsService  settings,
        IAudioService     audio,
        ILicenseService   license,
        ITelemetryService telemetry,
        IUpdateService    updates,
        IComPortService   comPorts,
        UsbDeviceDatabase usbDb,
        MainViewModel     mainVm)
    {
        _settings  = settings;
        _audio     = audio;
        _license   = license;
        _telemetry = telemetry;
        _updates   = updates;
        _comPorts  = comPorts;
        _usbDb     = usbDb;
        _mainVm    = mainVm;

        var s = settings.Current;

        // Save originals for Cancel revert
        _originalScaleFactor = s.ScaleFactor;
        _originalAlwaysOnTop = s.AlwaysOnTop;

        // Init pending fields from current settings
        _pendingScaleFactor          = s.ScaleFactor;
        _pendingAlwaysOnTop          = s.AlwaysOnTop;
        _pendingShowAllAudioDevices  = s.ShowAllAudioDevices;
        _pendingAutoSwitchOnConnect  = s.AutoSwitchOnConnect;
        _pendingAutoSwitchOnRemove   = s.AutoSwitchOnRemove;
        _pendingComPanelVisible      = s.ComPanelVisible;
        _pendingShowPuttyButton      = s.ShowPuttyButton;
        _savedShowPuttyButton        = s.ShowPuttyButton;
        _pendingPuttyExePath         = s.PuttyExePath ?? string.Empty;
        _pendingPreferredBaudRate    = s.PreferredBaudRate;
        _pendingShowUnrecognizedPorts = s.ShowUnrecognizedPorts;
        _pendingHighlightNewPorts    = s.HighlightNewPorts;
        _pendingLanguage             = s.Language ?? "en";
        _pendingTelemetryEnabled     = s.TelemetryEnabled;
        _pendingTelemetryFrequency   = s.TelemetryFrequency ?? "Monthly";
        _pendingAutoUpdateEnabled    = s.AutoUpdateEnabled;
        _pendingUpdateCheckFrequency = s.UpdateCheckFrequency ?? "Monthly";
        _pendingUpdateChannel        = s.UpdateChannel ?? "Stable";

        // Launch at startup: read from registry
        _pendingLaunchAtStartup = ReadLaunchAtStartupFromRegistry();

        // Warn if PuTTY path set but doesn't exist
        _showPuttyPathWarning = !string.IsNullOrEmpty(_pendingPuttyExePath)
                                && !File.Exists(_pendingPuttyExePath);

        // Commands
        OkCommand                 = new RelayCommand(Commit);
        CancelCommand             = new RelayCommand(Revert);
        BrowsePuttyCommand        = new RelayCommand(BrowsePuttyExe);
        PlayPcTestCommand         = new RelayCommand(
            () => _ = PlayTestToneAsync(isPc: true),
            ()  => PcPlaybackSelected?.IsAvailable == true);
        PlayRadioTestCommand      = new RelayCommand(
            () => _ = PlayTestToneAsync(isPc: false),
            ()  => RadioPlaybackSelected?.IsAvailable == true);
        OpenUsbDbFolderCommand    = new RelayCommand(OpenUsbDbFolder);
        CheckNowCommand           = new RelayCommand(
            () => _ = CheckForUpdateNowAsync(),
            ()  => !IsCheckingUpdate);
        OpenLogFolderCommand      = new RelayCommand(
            () => OpenFolder(_settings.LogFolderPath));
        OpenSettingsFolderCommand = new RelayCommand(
            () => OpenFolder(_settings.SettingsDirectory));
        ShowResetDialogCommand    = new RelayCommand(
            () => RequestResetDialog?.Invoke(this, EventArgs.Empty));
        BrowseLicenseFileCommand  = new RelayCommand(
            () => _ = BrowseLicenseFileAsync());
        GetLicensePersonalCommand = new RelayCommand(
            () => OpenUrl(BrandingInfo.AppURL));
        GetLicenseClubCommand     = new RelayCommand(
            () => OpenUrl(BrandingInfo.AppURL));
        GetDonateLinkCommand      = new RelayCommand(
            () => OpenUrl(BrandingInfo.DonationURL));

        // Init update status text
        _updateStatusText  = $"Up to date  ({BrandingInfo.FullVersion})";
        _updateStatusColor = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)); // green

        // Load collections
        LoadAudioDeviceLists();
        LoadLanguages();
        LoadComPorts();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Scale – pending + immediate-apply + radio buttons
    // ═════════════════════════════════════════════════════════════════════════

    public double PendingScaleFactor
    {
        get => _pendingScaleFactor;
        set
        {
            if (Math.Abs(_pendingScaleFactor - value) < 0.001) return;
            _pendingScaleFactor = value;
            _mainVm.ScaleFactor = value;
            OnPropertyChanged();
            NotifyAllScaleRadioButtons();
        }
    }

    private void NotifyAllScaleRadioButtons()
    {
        OnPropertyChanged(nameof(IsScaleS));
        OnPropertyChanged(nameof(IsScaleN));
        OnPropertyChanged(nameof(IsScaleL));
        OnPropertyChanged(nameof(IsScaleXL));
        OnPropertyChanged(nameof(IsScaleXXL));
    }

    public bool IsScaleS
    {
        get => Math.Abs(_pendingScaleFactor - 0.85) < 0.001;
        set { if (value) PendingScaleFactor = 0.85; }
    }
    public bool IsScaleN
    {
        get => Math.Abs(_pendingScaleFactor - 1.0) < 0.001;
        set { if (value) PendingScaleFactor = 1.0; }
    }
    public bool IsScaleL
    {
        get => Math.Abs(_pendingScaleFactor - 1.35) < 0.001;
        set { if (value) PendingScaleFactor = 1.35; }
    }
    public bool IsScaleXL
    {
        get => Math.Abs(_pendingScaleFactor - 1.75) < 0.001;
        set { if (value) PendingScaleFactor = 1.75; }
    }
    public bool IsScaleXXL
    {
        get => Math.Abs(_pendingScaleFactor - 2.25) < 0.001;
        set { if (value) PendingScaleFactor = 2.25; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Always on top – immediate-apply
    // ═════════════════════════════════════════════════════════════════════════

    public bool PendingAlwaysOnTop
    {
        get => _pendingAlwaysOnTop;
        set
        {
            if (SetField(ref _pendingAlwaysOnTop, value))
                _mainVm.IsAlwaysOnTop = value;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COM panel visible – cascades ShowPuttyButton
    // ═════════════════════════════════════════════════════════════════════════

    public bool PendingComPanelVisible
    {
        get => _pendingComPanelVisible;
        set
        {
            if (_pendingComPanelVisible == value) return;
            _pendingComPanelVisible = value;
            if (!value)
            {
                _savedShowPuttyButton = _pendingShowPuttyButton;
                PendingShowPuttyButton = false;
            }
            else
            {
                PendingShowPuttyButton = _savedShowPuttyButton;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsComSubSectionEnabled));
            OnPropertyChanged(nameof(IsPuttyPathEnabled));
        }
    }

    public bool PendingShowPuttyButton
    {
        get => _pendingShowPuttyButton;
        set
        {
            if (SetField(ref _pendingShowPuttyButton, value))
                OnPropertyChanged(nameof(IsPuttyPathEnabled));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PuTTY path warning
    // ═════════════════════════════════════════════════════════════════════════

    public string PendingPuttyExePath
    {
        get => _pendingPuttyExePath;
        set
        {
            if (SetField(ref _pendingPuttyExePath, value))
                ShowPuttyPathWarning = !string.IsNullOrEmpty(value) && !File.Exists(value);
        }
    }

    public bool ShowPuttyPathWarning
    {
        get => _showPuttyPathWarning;
        private set => SetField(ref _showPuttyPathWarning, value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Misc pending settings
    // ═════════════════════════════════════════════════════════════════════════

    public bool PendingShowAllAudioDevices
    {
        get => _pendingShowAllAudioDevices;
        set => SetField(ref _pendingShowAllAudioDevices, value);
    }

    public bool PendingAutoSwitchOnConnect
    {
        get => _pendingAutoSwitchOnConnect;
        set => SetField(ref _pendingAutoSwitchOnConnect, value);
    }

    public bool PendingAutoSwitchOnRemove
    {
        get => _pendingAutoSwitchOnRemove;
        set => SetField(ref _pendingAutoSwitchOnRemove, value);
    }

    public bool PendingShowUnrecognizedPorts
    {
        get => _pendingShowUnrecognizedPorts;
        set => SetField(ref _pendingShowUnrecognizedPorts, value);
    }

    public bool PendingHighlightNewPorts
    {
        get => _pendingHighlightNewPorts;
        set => SetField(ref _pendingHighlightNewPorts, value);
    }

    // ── Computed enablement helpers ───────────────────────────────────────────
    public bool IsComSubSectionEnabled => _pendingComPanelVisible;
    public bool IsPuttyPathEnabled     => _pendingComPanelVisible && _pendingShowPuttyButton;
    public bool ShowBetaChannelNote    => _pendingUpdateChannel == "Beta";
    public bool LicenseInstallHasError => !string.IsNullOrEmpty(_licenseInstallErrorText);

    // ── Portable mode text ────────────────────────────────────────────────────
    public string PortableModeText => _settings.IsPortableMode
        ? $"Portable mode — data folder: {_settings.SettingsDirectory}"
        : string.Empty;

    // ── Dropdown option lists ─────────────────────────────────────────────────
    public IReadOnlyList<string> UpdateFrequencyOptions  { get; } =
        new[] { "Daily", "Weekly", "Monthly" };

    public IReadOnlyList<string> UpdateChannelOptions    { get; } =
        new[] { "Stable", "Beta" };

    public IReadOnlyList<string> TelemetryFrequencyOptions { get; } =
        new[] { "Always", "Monthly", "Never" };

    public string PendingLanguage
    {
        get => _pendingLanguage;
        set => SetField(ref _pendingLanguage, value);
    }

    public bool PendingLaunchAtStartup
    {
        get => _pendingLaunchAtStartup;
        set => SetField(ref _pendingLaunchAtStartup, value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Baud rates
    // ═════════════════════════════════════════════════════════════════════════

    public IReadOnlyList<int> BaudRates { get; } =
        new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

    public int PendingPreferredBaudRate
    {
        get => _pendingPreferredBaudRate;
        set
        {
            if (SetField(ref _pendingPreferredBaudRate, value))
                RefreshComPortBaudDisplay();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Languages
    // ═════════════════════════════════════════════════════════════════════════

    public ObservableCollection<LanguageItem> Languages { get; } = [];

    private void LoadLanguages()
    {
        var supported = new[]
        {
            ("en", "English"),
            ("de", "Deutsch"),
            ("es", "Español"),
            ("fr", "Français"),
            ("ja", "日本語"),
        };

        Languages.Clear();
        foreach (var (code, name) in supported)
        {
            bool complete = code == "en" || HasEmbeddedResx(code);
            Languages.Add(new LanguageItem(code, name, complete));
        }
    }

    private static bool HasEmbeddedResx(string code)
    {
        var asm  = Assembly.GetExecutingAssembly();
        string target = $"Strings.{code}.resx";
        return asm.GetManifestResourceNames()
                  .Any(n => n.EndsWith(target, StringComparison.OrdinalIgnoreCase));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COM port live view
    // ═════════════════════════════════════════════════════════════════════════

    public ObservableCollection<ComPortDisplayItem> ComPortItems { get; } = [];
    public int UsbDbEntryCount => _usbDb.Count;

    private void LoadComPorts()
    {
        ComPortItems.Clear();
        var ports = _comPorts.GetComPorts();
        foreach (var p in ports)
        {
            bool isRecognized = !string.IsNullOrEmpty(p.Vid);
            string baudDisplay = p.SuggestedBaudRate > 0
                ? p.SuggestedBaudRate.ToString()
                : $"default {_pendingPreferredBaudRate}";
            ComPortItems.Add(new ComPortDisplayItem(p.PortName, p.FriendlyName, isRecognized, baudDisplay));
        }
    }

    private void RefreshComPortBaudDisplay()
    {
        // Rebuild items so "default X" reflects the new preferred baud rate
        LoadComPorts();
    }

    private void OpenUsbDbFolder()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Data");
        OpenFolder(folder);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Audio device lists
    // ═════════════════════════════════════════════════════════════════════════

    public ObservableCollection<AudioDeviceItem> PcPlaybackDevices    { get; } = [];
    public ObservableCollection<AudioDeviceItem> PcRecordingDevices   { get; } = [];
    public ObservableCollection<AudioDeviceItem> RadioPlaybackDevices { get; } = [];
    public ObservableCollection<AudioDeviceItem> RadioRecordingDevices{ get; } = [];

    private AudioDeviceItem? _pcPlaybackSelected;
    private AudioDeviceItem? _pcRecordingSelected;
    private AudioDeviceItem? _radioPlaybackSelected;
    private AudioDeviceItem? _radioRecordingSelected;

    public AudioDeviceItem? PcPlaybackSelected
    {
        get => _pcPlaybackSelected;
        set => SetField(ref _pcPlaybackSelected, value);
    }
    public AudioDeviceItem? PcRecordingSelected
    {
        get => _pcRecordingSelected;
        set => SetField(ref _pcRecordingSelected, value);
    }
    public AudioDeviceItem? RadioPlaybackSelected
    {
        get => _radioPlaybackSelected;
        set => SetField(ref _radioPlaybackSelected, value);
    }
    public AudioDeviceItem? RadioRecordingSelected
    {
        get => _radioRecordingSelected;
        set => SetField(ref _radioRecordingSelected, value);
    }

    private void LoadAudioDeviceLists()
    {
        var playback  = _audio.GetPlaybackDevices();
        var capture   = _audio.GetCaptureDevices();
        string? defaultPlayId = _audio.GetDefaultPlaybackId();
        string? defaultCapId  = _audio.GetDefaultCaptureId();

        var s = _settings.Current;

        // PC profile
        var pcProfile    = s.AudioProfiles.FirstOrDefault(p => p.Id == "pc");
        var radioProfile = s.AudioProfiles.FirstOrDefault(p => p.Id == "radio");

        BuildDeviceList(PcPlaybackDevices, playback, pcProfile?.Playback ?? string.Empty, defaultPlayId);
        BuildDeviceList(PcRecordingDevices, capture, pcProfile?.Recording ?? string.Empty, defaultCapId);
        BuildDeviceList(RadioPlaybackDevices, playback, radioProfile?.Playback ?? string.Empty, defaultPlayId);
        BuildDeviceList(RadioRecordingDevices, capture, radioProfile?.Recording ?? string.Empty, defaultCapId);

        _pcPlaybackSelected     = FindSelected(PcPlaybackDevices,     pcProfile?.Playback ?? string.Empty);
        _pcRecordingSelected    = FindSelected(PcRecordingDevices,    pcProfile?.Recording ?? string.Empty);
        _radioPlaybackSelected  = FindSelected(RadioPlaybackDevices,  radioProfile?.Playback ?? string.Empty);
        _radioRecordingSelected = FindSelected(RadioRecordingDevices, radioProfile?.Recording ?? string.Empty);
    }

    private static void BuildDeviceList(
        ObservableCollection<AudioDeviceItem> list,
        IReadOnlyList<AudioDeviceInfo> devices,
        string storedName,
        string? defaultId)
    {
        list.Clear();
        list.Add(new AudioDeviceItem("(none)", string.Empty));

        foreach (var d in devices)
        {
            string displayName = d.IsDefault ? $"★ {d.FriendlyName}" : d.FriendlyName;
            list.Add(new AudioDeviceItem(displayName, d.FriendlyName));
        }

        // If stored device not found in current list, add unavailable placeholder
        if (!string.IsNullOrEmpty(storedName)
            && !devices.Any(d => d.FriendlyName == storedName))
        {
            list.Add(new AudioDeviceItem($"{storedName} (not connected)", storedName, IsAvailable: false));
        }
    }

    private static AudioDeviceItem? FindSelected(
        ObservableCollection<AudioDeviceItem> list,
        string storedName)
    {
        if (string.IsNullOrEmpty(storedName))
            return list.FirstOrDefault();
        return list.FirstOrDefault(i => i.StorageName == storedName)
               ?? list.FirstOrDefault();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Test tone
    // ═════════════════════════════════════════════════════════════════════════

    private bool _isPcTestPlaying;
    private bool _isRadioTestPlaying;

    public bool IsPcTestPlaying
    {
        get => _isPcTestPlaying;
        private set => SetField(ref _isPcTestPlaying, value);
    }
    public bool IsRadioTestPlaying
    {
        get => _isRadioTestPlaying;
        private set => SetField(ref _isRadioTestPlaying, value);
    }

    private async Task PlayTestToneAsync(bool isPc)
    {
        if (isPc)  IsPcTestPlaying    = true;
        else       IsRadioTestPlaying = true;

        try
        {
            var selected = isPc ? PcPlaybackSelected : RadioPlaybackSelected;
            if (selected is null || !selected.IsAvailable) return;

            string storageName = selected.StorageName;
            MMDevice? device   = null;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                // Find device by matching friendly name since we store FriendlyName
                var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var d in allDevices)
                {
                    if (d.FriendlyName == storageName)
                    {
                        device = d;
                        break;
                    }
                }
            }
            catch { /* fall through — device unavailable */ }

            if (device is null) return;

            await Task.Run(async () =>
            {
                const int sampleRate    = 44100;
                const int durationMs    = 1000;
                const int fadeMs        = 100;
                const float frequency   = 1000f;

                var sineProvider = new SineToneProvider(frequency, sampleRate, durationMs, fadeMs);
                using var wasapi = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
                wasapi.Init(sineProvider);
                wasapi.Play();

                await Task.Delay(durationMs + 50);

                wasapi.Stop();
            });
        }
        catch { /* audio errors are silent */ }
        finally
        {
            if (isPc)  IsPcTestPlaying    = false;
            else       IsRadioTestPlaying = false;
        }
    }

    // ── Nested: sine wave sample provider ────────────────────────────────────
    private sealed class SineToneProvider : ISampleProvider
    {
        private readonly float   _frequency;
        private readonly int     _fadeInSamples;
        private readonly int     _fadeOutSamples;
        private readonly int     _totalSamples;
        private int              _sampleIndex;

        public WaveFormat WaveFormat { get; }

        public SineToneProvider(float frequency, int sampleRate, int durationMs, int fadeMs)
        {
            _frequency      = frequency;
            WaveFormat      = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _totalSamples   = sampleRate * durationMs  / 1000;
            _fadeInSamples  = sampleRate * fadeMs      / 1000;
            _fadeOutSamples = sampleRate * fadeMs      / 1000;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesWritten = 0;
            for (int i = 0; i < count && _sampleIndex < _totalSamples; i++, _sampleIndex++)
            {
                double t        = (double)_sampleIndex / WaveFormat.SampleRate;
                float  sample   = (float)Math.Sin(2.0 * Math.PI * _frequency * t);

                // Fade in
                if (_sampleIndex < _fadeInSamples)
                    sample *= (float)_sampleIndex / _fadeInSamples;

                // Fade out
                int samplesLeft = _totalSamples - _sampleIndex;
                if (samplesLeft < _fadeOutSamples)
                    sample *= (float)samplesLeft / _fadeOutSamples;

                buffer[offset + i] = sample * 0.5f; // half amplitude
                samplesWritten++;
            }
            return samplesWritten;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Update tab
    // ═════════════════════════════════════════════════════════════════════════

    private string _updateStatusText  = string.Empty;
    private Brush  _updateStatusColor = Brushes.White;
    private bool   _isCheckingUpdate;

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetField(ref _updateStatusText, value);
    }
    public Brush UpdateStatusColor
    {
        get => _updateStatusColor;
        private set => SetField(ref _updateStatusColor, value);
    }
    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        private set => SetField(ref _isCheckingUpdate, value);
    }

    public bool PendingAutoUpdateEnabled
    {
        get => _pendingAutoUpdateEnabled;
        set => SetField(ref _pendingAutoUpdateEnabled, value);
    }
    public string PendingUpdateCheckFrequency
    {
        get => _pendingUpdateCheckFrequency;
        set => SetField(ref _pendingUpdateCheckFrequency, value);
    }
    public string PendingUpdateChannel
    {
        get => _pendingUpdateChannel;
        set
        {
            if (SetField(ref _pendingUpdateChannel, value))
                OnPropertyChanged(nameof(ShowBetaChannelNote));
        }
    }

    public string UpdateCheckLastRun
    {
        get
        {
            string raw = _settings.Current.UpdateCheckLastRun;
            if (string.IsNullOrEmpty(raw)) return "Never";
            if (DateTimeOffset.TryParse(raw, out var dt))
                return dt.ToLocalTime().ToString("g");
            return raw;
        }
    }

    private async Task CheckForUpdateNowAsync()
    {
        IsCheckingUpdate  = true;
        UpdateStatusText  = "Checking for updates...";
        UpdateStatusColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)); // amber

        try
        {
            var result = await Task.Run(() => _updates.CheckForUpdateAsync(force: true));

            if (result is not null)
            {
                UpdateStatusText  = $"Update available: {result.Version}";
                UpdateStatusColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)); // red
            }
            else
            {
                UpdateStatusText  = $"Up to date  ({BrandingInfo.FullVersion})";
                UpdateStatusColor = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58)); // green
            }
            OnPropertyChanged(nameof(UpdateCheckLastRun));
        }
        catch
        {
            UpdateStatusText  = "Update check failed";
            UpdateStatusColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)); // red
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Privacy tab
    // ═════════════════════════════════════════════════════════════════════════

    public bool PendingTelemetryEnabled
    {
        get => _pendingTelemetryEnabled;
        set => SetField(ref _pendingTelemetryEnabled, value);
    }

    public string PendingTelemetryFrequency
    {
        get => _pendingTelemetryFrequency;
        set => SetField(ref _pendingTelemetryFrequency, value);
    }

    public int PendingReportCount => _telemetry.GetPendingReports().Count;

    // ═════════════════════════════════════════════════════════════════════════
    // License tab pass-throughs
    // ═════════════════════════════════════════════════════════════════════════

    public ReleaseChannel Channel        => ChannelInfo.Channel;
    public bool           IsAlpha        => ChannelInfo.Channel == ReleaseChannel.Alpha;
    public bool           IsBeta         => ChannelInfo.Channel == ReleaseChannel.Beta;
    public bool           IsStable       => ChannelInfo.Channel == ReleaseChannel.Stable;
    public int?           DaysRemaining  => BrandingInfo.DaysRemaining;
    public bool           HasExpiry      => BrandingInfo.DaysRemaining is not null;
    public bool           BuildDateStamped => !string.IsNullOrEmpty(BrandingInfo.BuildDate);
    public string         FullVersion    => BrandingInfo.FullVersion;
    public bool           ShowLicenseKeyInstall => !ChannelInfo.UnlockAllForTesting;

    public string LicenseTierDisplayName => _license.Current.Tier switch
    {
        LicenseTier.Free     => "Free (MIT)",
        LicenseTier.Personal => "Personal",
        LicenseTier.Club     => "Club",
        LicenseTier.EmComm   => "EMCOMM",
        _                    => "Free (MIT)"
    };

    public string LicenseeDisplay => _license.Current.Licensee ?? string.Empty;

    private string _licenseInstallErrorText = string.Empty;
    private bool   _licenseInstallSuccess;

    public string LicenseInstallErrorText
    {
        get => _licenseInstallErrorText;
        private set
        {
            if (SetField(ref _licenseInstallErrorText, value))
                OnPropertyChanged(nameof(LicenseInstallHasError));
        }
    }
    public bool LicenseInstallSuccess
    {
        get => _licenseInstallSuccess;
        private set => SetField(ref _licenseInstallSuccess, value);
    }

    private async Task BrowseLicenseFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title            = "Select License File",
            Filter           = "PortPane License|*.portpane",
            CheckFileExists  = true,
        };

        if (dlg.ShowDialog() == true)
            await InstallLicenseFromFileAsync(dlg.FileName);
    }

    /// <summary>Called from code-behind on drag-drop of a .portpane file.</summary>
    public async Task LicenseDropFile(string filePath)
        => await InstallLicenseFromFileAsync(filePath);

    private async Task InstallLicenseFromFileAsync(string filePath)
    {
        LicenseInstallErrorText = string.Empty;
        LicenseInstallSuccess   = false;

        var result = await _license.ValidateAndInstallAsync(filePath);

        if (result.Success)
        {
            LicenseInstallSuccess = true;
            OnPropertyChanged(nameof(LicenseTierDisplayName));
            OnPropertyChanged(nameof(LicenseeDisplay));
            OnPropertyChanged(nameof(FooterText));
        }
        else
        {
            LicenseInstallErrorText = result.ErrorMessage switch
            {
                "file_unreadable"    => "Could not read the license file.",
                "wrong_app"          => "This license is not for PortPane.",
                "invalid_signature"  => "License signature is invalid.",
                "expired"            => "This license has expired.",
                _                    => $"License error: {result.ErrorMessage}"
            };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Footer
    // ═════════════════════════════════════════════════════════════════════════

    public string FooterText
    {
        get
        {
            string channelOrTier = ChannelInfo.Channel switch
            {
                ReleaseChannel.Alpha  => "Alpha Build",
                ReleaseChannel.Beta   => $"Beta · {LicenseTierDisplayName}",
                ReleaseChannel.Stable => LicenseTierDisplayName,
                _                     => LicenseTierDisplayName
            };
            return $"{BrandingInfo.AppName} {BrandingInfo.FullVersion} · {channelOrTier} · {BrandingInfo.AuthorCallsign}";
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Commit / Revert
    // ═════════════════════════════════════════════════════════════════════════

    private void Commit()
    {
        var s = _settings.Current;

        s.ScaleFactor             = _pendingScaleFactor;
        s.AlwaysOnTop             = _pendingAlwaysOnTop;
        s.ShowAllAudioDevices     = _pendingShowAllAudioDevices;
        s.AutoSwitchOnConnect     = _pendingAutoSwitchOnConnect;
        s.AutoSwitchOnRemove      = _pendingAutoSwitchOnRemove;
        s.ComPanelVisible         = _pendingComPanelVisible;
        s.ShowPuttyButton         = _pendingShowPuttyButton;
        s.PuttyExePath            = _pendingPuttyExePath;
        s.PreferredBaudRate       = _pendingPreferredBaudRate;
        s.ShowUnrecognizedPorts   = _pendingShowUnrecognizedPorts;
        s.HighlightNewPorts       = _pendingHighlightNewPorts;
        s.Language                = _pendingLanguage;
        s.LaunchAtStartup         = _pendingLaunchAtStartup;
        s.TelemetryEnabled        = _pendingTelemetryEnabled;
        s.TelemetryFrequency      = _pendingTelemetryFrequency;
        s.AutoUpdateEnabled       = _pendingAutoUpdateEnabled;
        s.UpdateCheckFrequency    = _pendingUpdateCheckFrequency;
        s.UpdateChannel           = _pendingUpdateChannel;

        // Commit audio selections into AudioProfiles
        var pcProfile    = s.AudioProfiles.FirstOrDefault(p => p.Id == "pc");
        var radioProfile = s.AudioProfiles.FirstOrDefault(p => p.Id == "radio");

        if (pcProfile is not null)
        {
            pcProfile.Playback  = PcPlaybackSelected?.StorageName  ?? string.Empty;
            pcProfile.Recording = PcRecordingSelected?.StorageName ?? string.Empty;
        }
        if (radioProfile is not null)
        {
            radioProfile.Playback  = RadioPlaybackSelected?.StorageName  ?? string.Empty;
            radioProfile.Recording = RadioRecordingSelected?.StorageName ?? string.Empty;
        }

        // Write LaunchAtStartup to registry
        WriteLaunchAtStartupToRegistry(_pendingLaunchAtStartup);

        _settings.Save();
    }

    private void Revert()
    {
        // Restore originals to settings and mainVm
        _settings.Current.ScaleFactor = _originalScaleFactor;
        _settings.Current.AlwaysOnTop = _originalAlwaysOnTop;
        _mainVm.ScaleFactor   = _originalScaleFactor;
        _mainVm.IsAlwaysOnTop = _originalAlwaysOnTop;

        // Reset pending fields visually
        _pendingScaleFactor = _originalScaleFactor;
        _pendingAlwaysOnTop = _originalAlwaysOnTop;
        OnPropertyChanged(nameof(PendingScaleFactor));
        OnPropertyChanged(nameof(PendingAlwaysOnTop));
        NotifyAllScaleRadioButtons();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Registry helpers (LaunchAtStartup)
    // ═════════════════════════════════════════════════════════════════════════

    private const string StartupRegistryKey =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static bool ReadLaunchAtStartupFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
            return key?.GetValue(BrandingInfo.AppName) is not null;
        }
        catch { return false; }
    }

    private static void WriteLaunchAtStartupToRegistry(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                                 ?? Environment.ProcessPath
                                 ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(BrandingInfo.AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(BrandingInfo.AppName, throwOnMissingValue: false);
            }
        }
        catch { /* registry write failure is non-fatal */ }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Utility helpers
    // ═════════════════════════════════════════════════════════════════════════

    private void BrowsePuttyExe()
    {
        var dlg = new OpenFileDialog
        {
            Title           = "Select PuTTY executable",
            Filter          = "Executable|putty.exe;*.exe|All files|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() == true)
            PendingPuttyExePath = dlg.FileName;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }
        catch { /* non-fatal */ }
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* non-fatal */ }
    }
}
