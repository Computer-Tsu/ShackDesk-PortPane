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
    private readonly IAudioService   _audio;
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

    public bool   IsRadioMode         => ActiveProfile == "Radio";
    public string ProfileButtonLabel  => IsRadioMode ? "Radio Mode  ✦" : "PC Mode";
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
    }

    private async void SwitchProfile()
    {
        IsSwitching = true;
        SwitchProfileCommand.RaiseCanExecuteChanged();
        try
        {
            string newProfile = IsRadioMode ? "PC" : "Radio";
            Log.Information("Switching audio profile: {From} → {To}", ActiveProfile, newProfile);

            var profileId = newProfile == "Radio" ? "radio" : "pc";
            var profile   = _settings.Current.AudioProfiles.FirstOrDefault(p => p.Id == profileId);

            string? playbackId = FindDeviceId(profile?.Playback  ?? string.Empty);
            string? captureId  = FindDeviceId(profile?.Recording ?? string.Empty);

            bool ok = true;
            if (!string.IsNullOrEmpty(playbackId)) ok &= _audio.SetDefaultDevice(playbackId);
            if (!string.IsNullOrEmpty(captureId))  ok &= _audio.SetDefaultDevice(captureId);

            if (ok || (string.IsNullOrEmpty(playbackId) && string.IsNullOrEmpty(captureId)))
            {
                ActiveProfile = newProfile;
                _settings.Current.ActiveProfileId = newProfile.ToLowerInvariant();
                _settings.Save();
                ShowStatus($"Switched to {newProfile} mode");
            }
            else
            {
                ShowStatus("Profile switch incomplete — check Settings");
            }

            await Task.Delay(200); // small pause so the UI updates feel snappy
            Refresh();
        }
        finally
        {
            IsSwitching = false;
            SwitchProfileCommand.RaiseCanExecuteChanged();
        }
    }

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
    public string Id              { get; }
    public string FriendlyName   { get; }
    public bool   IsDefault      { get; }
    public bool   IsUsb          { get; }
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
