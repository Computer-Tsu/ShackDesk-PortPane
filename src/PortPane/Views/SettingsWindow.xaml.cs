using System.IO;
using System.Windows;
using PortPane.Services;
using PortPane.ViewModels;

namespace PortPane.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly ISettingsService  _settings;

    public SettingsWindow(SettingsViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm         = viewModel;
        _settings   = settings;

        // Save on close
        Closed += (_, _) => _settings.Save();
    }

    // License file drag-drop
    private void LicenseKeyBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string file = files[0];
            if (Path.GetExtension(file).Equals(".portpane", StringComparison.OrdinalIgnoreCase))
            {
                _vm.LicenseKeyInput = File.ReadAllText(file).Trim();
            }
        }
    }
}
