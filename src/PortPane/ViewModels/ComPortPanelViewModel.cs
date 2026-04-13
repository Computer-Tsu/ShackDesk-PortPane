using System.Collections.ObjectModel;
using System.Windows;
using PortPane.Services;
using Serilog;

namespace PortPane.ViewModels;

/// <summary>
/// ViewModel for the COM port panel.
/// Handles port listing, classification, baud selection, clipboard copy, and PuTTY launch.
/// </summary>
public sealed class ComPortPanelViewModel : ViewModelBase
{
    private readonly IComPortService  _comPorts;
    private readonly IPuttyService    _putty;
    private readonly ISettingsService _settings;

    private ComPortRowViewModel? _selectedPort;
    private int                  _selectedBaudRate;
    private string               _copyConfirmation = string.Empty;
    private bool                 _showCopyConfirmation;

    public ObservableCollection<ComPortRowViewModel> Ports       { get; } = [];
    public ObservableCollection<int>                 BaudRates   { get; } = [4800, 9600, 19200, 38400, 57600, 115200];

    public ComPortRowViewModel? SelectedPort
    {
        get => _selectedPort;
        set
        {
            SetField(ref _selectedPort, value);
            OnPropertyChanged(nameof(CanLaunchPutty));
            if (value is not null)
                SelectedBaudRate = value.SuggestedBaudRate;
        }
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set
        {
            SetField(ref _selectedBaudRate, value);
            _settings.Current.PreferredBaudRate = value;
        }
    }

    public bool IsPuttyAvailable      => _putty.IsPuttyAvailable;
    public bool ShowPuttyControls     => _settings.Current.ShowPuttyButton && IsPuttyAvailable;
    public bool CanLaunchPutty        => ShowPuttyControls && SelectedPort is not null && !SelectedPort.IsGhost;

    public string CopyConfirmation
    {
        get => _copyConfirmation;
        private set => SetField(ref _copyConfirmation, value);
    }

    public bool ShowCopyConfirmation
    {
        get => _showCopyConfirmation;
        private set => SetField(ref _showCopyConfirmation, value);
    }

    public RelayCommand         RefreshCommand       { get; }
    public RelayCommand<string> CopyPortNameCommand  { get; }
    public RelayCommand         OpenInPuttyCommand   { get; }

    public ComPortPanelViewModel(
        IComPortService comPorts,
        IPuttyService   putty,
        ISettingsService settings)
    {
        _comPorts = comPorts;
        _putty    = putty;
        _settings = settings;

        _selectedBaudRate = settings.Current.PreferredBaudRate;

        RefreshCommand      = new RelayCommand(Refresh);
        CopyPortNameCommand = new RelayCommand<string>(CopyPortName);
        OpenInPuttyCommand  = new RelayCommand(OpenInPutty, () => CanLaunchPutty);

        Refresh();
    }

    public void Refresh()
    {
        Ports.Clear();
        foreach (var port in _comPorts.GetComPorts())
            Ports.Add(new ComPortRowViewModel(port));
        Log.Debug("COM port panel refreshed: {Count} ports ({Ghosts} ghost)",
            Ports.Count, Ports.Count(p => p.IsGhost));
    }

    public void RefreshPuttySettings()
    {
        OnPropertyChanged(nameof(ShowPuttyControls));
        OnPropertyChanged(nameof(CanLaunchPutty));
        OpenInPuttyCommand.RaiseCanExecuteChanged();
    }

    private async void CopyPortName(string? portName)
    {
        if (string.IsNullOrEmpty(portName)) return;
        try
        {
            Clipboard.SetText(portName);
            Log.Debug("Copied to clipboard: {Port}", portName);
            CopyConfirmation    = $"Copied: {portName}";
            ShowCopyConfirmation = true;
            await Task.Delay(2000);
            ShowCopyConfirmation = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Clipboard copy failed");
        }
    }

    private void OpenInPutty()
    {
        if (SelectedPort is null) return;
        _putty.Launch(SelectedPort.PortName, SelectedBaudRate);
    }
}

/// <summary>
/// Per-row ViewModel for one COM port entry.
/// </summary>
public sealed class ComPortRowViewModel : ViewModelBase
{
    public string  PortName          { get; }
    public string  FriendlyName      { get; }
    public string? Vid               { get; }
    public string? Pid               { get; }
    public bool    IsRadioInterface  { get; }
    public bool    IsGhost           { get; }
    public int     SuggestedBaudRate { get; }

    /// <summary>
    /// Ghost ports show a tooltip explaining they can be removed via Device Manager.
    /// </summary>
    public string GhostTooltip => IsGhost
        ? "Previously connected device — no longer present. Remove in Device Manager if desired."
        : string.Empty;

    public string DisplayLabel => IsGhost ? $"{PortName}  (disconnected)" : PortName;

    public ComPortRowViewModel(ComPortInfo info)
    {
        PortName         = info.PortName;
        FriendlyName     = info.FriendlyName;
        Vid              = info.Vid;
        Pid              = info.Pid;
        IsRadioInterface = info.IsRadioInterface;
        IsGhost          = info.IsGhost;
        SuggestedBaudRate = info.SuggestedBaudRate;
    }
}
