using System.Collections.ObjectModel;
using PortPane.Services;
using Serilog;

namespace PortPane.ViewModels;

/// <summary>
/// ViewModel for the audio device panel.
/// Manages playback/capture device lists, default device display,
/// and PC / Radio profile switching.
/// </summary>
public sealed class AudioPanelViewModel : ViewModelBase
{
    private readonly IAudioService    _audio;
    private readonly ISettingsService _settings;

    private string _defaultPlaybackName  = "None";
    private string _defaultCaptureName   = "None";
    private string _activeProfile        = "PC";
    private bool   _isSwitching;
    private string _statusMessage        = string.Empty;
    private bool   _hasStatusMessage;

    public ObservableCollection<AudioDeviceViewModel> PlaybackDevices { get; } = [];
    public ObservableCollection<AudioDeviceViewModel> CaptureDevices  { get; } = [];
    public ObservableCollection<AudioDeviceViewModel> UsbPlayback     { get; } = [];
    public ObservableCollection<AudioDeviceViewModel> UsbCapture      { get; } = [];
    public ObservableCollection<AudioDeviceViewModel> BuiltinPlayback { get; } = [];
    public ObservableCollection<AudioDeviceViewModel> BuiltinCapture  { get; } = [];

    public string DefaultPlaybackName
    {
        get => _defaultPlaybackName;
        private set => SetField(ref _defaultPlaybackName, value);
    }

    public string DefaultCaptureName
    {
        get => _defaultCaptureName;
        private set => SetField(ref _defaultCaptureName, value);
    }

    public string ActiveProfile
    {
        get => _activeProfile;
        private set
        {
            SetField(ref _activeProfile, value);
            OnPropertyChanged(nameof(IsRadioMode));
            OnPropertyChanged(nameof(ProfileButtonLabel));
            OnPropertyChanged(nameof(ProfileButtonTooltip));
        }
    }

    public bool   IsRadioMode          => ActiveProfile == "Radio";
    public string ProfileButtonLabel   => IsRadioMode ? "Radio Mode  ✦" : "PC Mode";
    public string ProfileButtonTooltip => IsRadioMode
        ? "Currently in Radio Mode — click to switch to PC Mode"
        : "Currently in PC Mode — click to switch to Radio Mode";

    public bool IsSwitching
    {
        get => _isSwitching;
        private set => SetField(ref _isSwitching, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool HasStatusMessage
    {
        get => _hasStatusMessage;
        private set => SetField(ref _hasStatusMessage, value);
    }

    public RelayCommand SwitchProfileCommand { get; }
    public RelayCommand RefreshCommand       { get; }

    public AudioPanelViewModel(IAudioService audio, ISettingsService settings)
    {
        _audio    = audio;
        _settings = settings;

        SwitchProfileCommand = new RelayCommand(SwitchProfile, () => !IsSwitching);
        RefreshCommand       = new RelayCommand(Refresh);

        ActiveProfile = _settings.Current.ActiveProfileId == "radio" ? "Radio" : "PC";
        Refresh();
    }

    public void Refresh()
    {
        var playback = _audio.GetPlaybackDevices();
        var capture  = _audio.GetCaptureDevices();

        Repopulate(PlaybackDevices, playback.Select(d => new AudioDeviceViewModel(d)));
        Repopulate(CaptureDevices,  capture.Select(d => new AudioDeviceViewModel(d)));

        Repopulate(UsbPlayback,     playback.Where(d => d.IsUsb).Select(d => new AudioDeviceViewModel(d)));
        Repopulate(BuiltinPlayback, playback.Where(d => !d.IsUsb).Select(d => new AudioDeviceViewModel(d)));
        Repopulate(UsbCapture,      capture.Where(d => d.IsUsb).Select(d => new AudioDeviceViewModel(d)));
        Repopulate(BuiltinCapture,  capture.Where(d => !d.IsUsb).Select(d => new AudioDeviceViewModel(d)));

        DefaultPlaybackName = playback.FirstOrDefault(d => d.IsDefault)?.FriendlyName ?? "None";
        DefaultCaptureName  = capture.FirstOrDefault(d => d.IsDefault)?.FriendlyName  ?? "None";

        SyncProfileStateFromWindows();
    }

    /// <summary>
    /// Compares the current Windows default audio devices against the saved
    /// PC and Radio profiles. If Windows has drifted to a different profile
    /// (e.g. because the OS auto-switched on USB arrival), updates ActiveProfile
    /// to reflect reality so the button label and switch logic stay accurate.
    /// </summary>
    private void SyncProfileStateFromWindows()
    {
        string? defaultPlayId = _audio.GetDefaultPlaybackId();
        string? defaultCapId  = _audio.GetDefaultCaptureId();

        foreach (var profile in _settings.Current.AudioProfiles)
        {
            bool hasPlayback  = !string.IsNullOrEmpty(profile.Playback);
            bool hasCapture   = !string.IsNullOrEmpty(profile.Recording);

            // Skip profiles with no devices configured — they match everything trivially.
            if (!hasPlayback && !hasCapture) continue;

            string? profilePlayId = FindDeviceId(profile.Playback);
            string? profileCapId  = FindDeviceId(profile.Recording);

            bool playbackMatches = !hasPlayback || profilePlayId == defaultPlayId;
            bool captureMatches  = !hasCapture  || profileCapId  == defaultCapId;

            if (!playbackMatches || !captureMatches) continue;

            string matched = profile.Id == "radio" ? "Radio" : "PC";
            if (matched != ActiveProfile)
            {
                Log.Information(
                    "Audio profile synced from Windows defaults: {Old} → {New} " +
                    "(playback: {Play}, capture: {Cap})",
                    ActiveProfile, matched, DefaultPlaybackName, DefaultCaptureName);
                ActiveProfile = matched;
                _settings.Current.ActiveProfileId = profile.Id;
                _settings.Save();
            }
            return;
        }
    }

    /// <summary>
    /// Switches to the specified profile by ID ("pc" or "radio").
    /// Used by both the manual button and the auto-switch hotplug path.
    /// Returns true if the switch completed successfully.
    /// </summary>
    public async Task<bool> SwitchToProfileAsync(string profileId)
    {
        if (IsSwitching) return false;

        IsSwitching = true;
        SwitchProfileCommand.RaiseCanExecuteChanged();
        try
        {
            string newProfile = profileId == "radio" ? "Radio" : "PC";
            Log.Information("Switching audio profile: {From} → {To}", ActiveProfile, newProfile);

            var profile = _settings.Current.AudioProfiles.FirstOrDefault(p => p.Id == profileId);

            string? playbackId = FindDeviceId(profile?.Playback ?? string.Empty);
            string? captureId  = FindDeviceId(profile?.Recording ?? string.Empty);

            // Step 4: if no exact name match on Radio profile, fall back to the first
            // connected device classified as a radio interface.
            if (profileId == "radio" && playbackId is null && captureId is null)
            {
                var allDevices = _audio.GetAllDevices();
                playbackId = allDevices.FirstOrDefault(d => d.IsRadioInterface && d.IsUsb)?.Id;
                captureId  = allDevices.FirstOrDefault(d => d.IsRadioInterface && d.IsUsb
                                 && d.Id != playbackId)?.Id
                             ?? playbackId;

                if (playbackId is not null)
                    Log.Information("Radio profile fallback: using first detected radio interface");
            }

            bool ok = true;
            if (!string.IsNullOrEmpty(playbackId)) ok &= _audio.SetDefaultDevice(playbackId);
            if (!string.IsNullOrEmpty(captureId))  ok &= _audio.SetDefaultDevice(captureId);

            if (ok || (string.IsNullOrEmpty(playbackId) && string.IsNullOrEmpty(captureId)))
            {
                ActiveProfile = newProfile;
                _settings.Current.ActiveProfileId = profileId;
                _settings.Save();
                ShowStatus($"Switched to {newProfile} mode");
                await Task.Delay(200);
                Refresh();
                return true;
            }

            ShowStatus("Profile switch incomplete — check Settings");
            return false;
        }
        finally
        {
            IsSwitching = false;
            SwitchProfileCommand.RaiseCanExecuteChanged();
        }
    }

    private void SwitchProfile() => _ = SwitchToProfileAsync(IsRadioMode ? "pc" : "radio");

    private string? FindDeviceId(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName)) return null;
        return _audio.GetAllDevices()
            .FirstOrDefault(d => d.FriendlyName.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    private async void ShowStatus(string message)
    {
        StatusMessage    = message;
        HasStatusMessage = true;
        await Task.Delay(3000);
        HasStatusMessage = false;
    }

    private static void Repopulate<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }
}

/// <summary>
/// Lightweight per-device row ViewModel for the audio lists.
/// </summary>
public sealed class AudioDeviceViewModel : ViewModelBase
{
    public string Id               { get; }
    public string FriendlyName    { get; }
    public bool   IsDefault       { get; }
    public bool   IsUsb           { get; }
    public bool   IsRadioInterface { get; }

    public AudioDeviceViewModel(AudioDeviceInfo d)
    {
        Id               = d.Id;
        FriendlyName     = d.FriendlyName;
        IsDefault        = d.IsDefault;
        IsUsb            = d.IsUsb;
        IsRadioInterface = d.IsRadioInterface;
    }
}
