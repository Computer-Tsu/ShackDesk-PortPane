using System.Windows;
using System.Windows.Input;
using PortPane.Services;
using Serilog;

namespace PortPane.ViewModels;

/// <summary>
/// Root ViewModel for MainWindow.
/// Coordinates panels, chrome reveal, scaling, and cross-cutting concerns.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ISettingsService  _settings;
    private readonly IHotplugService   _hotplug;
    private readonly ILicenseService   _license;
    private readonly IDeviceTelemetryService _deviceTelemetry;
    private readonly IUpdateService    _updates;

    private bool   _isChromeVisible;
    private bool   _isAlwaysOnTop;
    private double _scaleFactor;
    private bool   _hasUpdate;
    private string _updateText          = string.Empty;
    private UpdateAvailable? _pendingUpdate;

    // Sub-panel ViewModels (injected, not created here)
    public AudioPanelViewModel  Audio   { get; }
    public ComPortPanelViewModel ComPorts { get; }

    public bool IsChromeVisible
    {
        get => _isChromeVisible;
        set => SetField(ref _isChromeVisible, value);
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            SetField(ref _isAlwaysOnTop, value);
            _settings.Current.AlwaysOnTop = value;
        }
    }

    public double ScaleFactor
    {
        get => _scaleFactor;
        set
        {
            SetField(ref _scaleFactor, value);
            _settings.Current.ScaleFactor = value;
        }
    }

    public bool HasUpdate
    {
        get => _hasUpdate;
        private set => SetField(ref _hasUpdate, value);
    }

    public string UpdateText
    {
        get => _updateText;
        private set => SetField(ref _updateText, value);
    }

    public bool IsComPanelVisible
    {
        get => _settings.Current.ComPanelVisible;
        set
        {
            _settings.Current.ComPanelVisible = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get
        {
            string tier = _license.Current.Tier switch
            {
                LicenseTier.Club    => $"  [{_license.Current.Licensee}]",
                LicenseTier.EmComm  => $"  [{_license.Current.Licensee}]",
                _                   => string.Empty
            };
            return $"{BrandingInfo.FullName}  {BrandingInfo.FullVersion}{tier}";
        }
    }

    // ── Channel / build info (alpha/beta only) ────────────────────────────────

    public bool   IsPreRelease     => ChannelInfo.Channel != ReleaseChannel.Stable;
    public string ChannelBadgeText => ChannelInfo.Channel == ReleaseChannel.Alpha ? "ALPHA" : "BETA";
    public string BuildExpiryText
    {
        get
        {
            int? days = BrandingInfo.DaysRemaining;
            if (days == null) return string.Empty;
            return days switch
            {
                0 => "Expires today",
                1 => "1 day remaining",
                _ => $"{days} days remaining"
            };
        }
    }

    // Commands
    public ICommand RefreshAllCommand      { get; }
    public ICommand ToggleChromeCommand    { get; }
    public ICommand ToggleAlwaysOnTopCommand { get; }
    public ICommand ToggleComPanelCommand  { get; }
    public ICommand ApplyUpdateCommand     { get; }
    public ICommand DismissUpdateCommand   { get; }
    public RelayCommand<double> SetScaleCommand { get; }

    public MainViewModel(
        AudioPanelViewModel     audio,
        ComPortPanelViewModel   comPorts,
        ISettingsService        settings,
        IHotplugService         hotplug,
        ILicenseService         license,
        IDeviceTelemetryService deviceTelemetry,
        IUpdateService          updates)
    {
        Audio      = audio;
        ComPorts   = comPorts;
        _settings  = settings;
        _hotplug   = hotplug;
        _license   = license;
        _deviceTelemetry = deviceTelemetry;
        _updates   = updates;

        _isAlwaysOnTop = settings.Current.AlwaysOnTop;
        _scaleFactor   = settings.Current.ScaleFactor;

        RefreshAllCommand        = new RelayCommand(RefreshAll);
        ToggleChromeCommand      = new RelayCommand(() => IsChromeVisible = !IsChromeVisible);
        ToggleAlwaysOnTopCommand = new RelayCommand(() => IsAlwaysOnTop   = !IsAlwaysOnTop);
        ToggleComPanelCommand    = new RelayCommand(() => IsComPanelVisible = !IsComPanelVisible);
        ApplyUpdateCommand       = new RelayCommand(ApplyUpdate, () => _pendingUpdate is not null);
        DismissUpdateCommand     = new RelayCommand(() => { HasUpdate = false; _pendingUpdate = null; });
        SetScaleCommand          = new RelayCommand<double>(s => ScaleFactor = s);

        // Wire hotplug → refresh
        _hotplug.DeviceArrived += (_, _) => RefreshAfterHotplug("hotplug_arrived");
        _hotplug.DeviceRemoved += (_, _) => RefreshAfterHotplug("hotplug_removed");
        _hotplug.Start();
    }

    public void NotifyUpdate(UpdateAvailable info)
    {
        _pendingUpdate = info;
        UpdateText     = $"PortPane {info.Version} is available";
        HasUpdate      = true;
    }

    private void RefreshAll()
    {
        Audio.Refresh();
        ComPorts.Refresh();
    }

    private void RefreshAfterHotplug(string trigger)
    {
        App.Current.Dispatcher.Invoke(RefreshAll);
        _ = Task.Run(() => _deviceTelemetry.ReportDeviceSnapshotAsync(trigger));

        // Auto-switch audio profile on USB radio device arrival / removal.
        // Only acts when the user has enabled the corresponding setting.
        if (trigger == "hotplug_arrived" && _settings.Current.AutoSwitchOnConnect)
            App.Current.Dispatcher.Invoke(TryAutoSwitchToRadio);
        else if (trigger == "hotplug_removed" && _settings.Current.AutoSwitchOnRemove)
            App.Current.Dispatcher.Invoke(TryAutoSwitchToPc);
    }

    /// <summary>
    /// Switches to Radio profile when a USB radio interface arrives.
    /// Only runs when the user has enabled "auto-switch on connect" in Settings.
    /// Requires a radio device to be present and a Radio profile to be configured.
    /// </summary>
    private void TryAutoSwitchToRadio()
    {
        if (!_settings.Current.AutoSwitchOnConnect) return;
        if (Audio.IsRadioMode) return;

        bool radioProfileConfigured = _settings.Current.AudioProfiles
            .FirstOrDefault(p => p.Id == "radio") is { } rp
            && (!string.IsNullOrEmpty(rp.Playback) || !string.IsNullOrEmpty(rp.Recording));

        bool radioDevicePresent = Audio.UsbPlayback.Any(d => d.IsRadioInterface)
                               || Audio.UsbCapture.Any(d => d.IsRadioInterface);

        if (radioDevicePresent && radioProfileConfigured)
        {
            Log.Information("Auto-switching to Radio profile — radio USB device arrived");
            _ = Audio.SwitchToProfileAsync("radio");
        }
    }

    /// <summary>
    /// Switches back to PC profile when the last radio USB device is removed.
    /// Only runs when the user has enabled "auto-switch on remove" in Settings.
    /// </summary>
    private void TryAutoSwitchToPc()
    {
        if (!_settings.Current.AutoSwitchOnRemove) return;
        if (!Audio.IsRadioMode) return;

        bool radioDeviceStillPresent = Audio.UsbPlayback.Any(d => d.IsRadioInterface)
                                    || Audio.UsbCapture.Any(d => d.IsRadioInterface);

        if (!radioDeviceStillPresent)
        {
            Log.Information("Auto-switching to PC profile — radio USB device removed");
            _ = Audio.SwitchToProfileAsync("pc");
        }
    }

    private async void ApplyUpdate()
    {
        if (_pendingUpdate is null) return;
        var pending = _pendingUpdate;
        try
        {
            HasUpdate = false;
            await _updates.ApplyUpdateAsync(pending);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update apply failed — version: {Version}", pending.Version);
            HasUpdate = true; // restore banner so user can retry
        }
    }
}

/// <summary>Generic typed RelayCommand for the MainViewModel.</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter)    => _execute((T?)parameter);

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
