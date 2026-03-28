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
            return $"{BrandingInfo.FullName}  {BrandingInfo.Version}{tier}";
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
        AudioPanelViewModel    audio,
        ComPortPanelViewModel  comPorts,
        ISettingsService       settings,
        IHotplugService        hotplug,
        ILicenseService        license)
    {
        Audio      = audio;
        ComPorts   = comPorts;
        _settings  = settings;
        _hotplug   = hotplug;
        _license   = license;

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
        _hotplug.DeviceArrived += (_, _) => App.Current.Dispatcher.Invoke(RefreshAll);
        _hotplug.DeviceRemoved += (_, _) => App.Current.Dispatcher.Invoke(RefreshAll);
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

    private async void ApplyUpdate()
    {
        if (_pendingUpdate is null) return;
        try
        {
            object? updateSvc = App.Current.MainWindow?.DataContext is MainViewModel
                ? null : null; // resolved via DI by caller
            // The update service is invoked from the view (Apply button) — resolved via DI in view
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update apply failed");
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
