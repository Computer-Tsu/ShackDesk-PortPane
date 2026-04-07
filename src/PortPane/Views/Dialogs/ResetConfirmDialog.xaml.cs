using System.Windows;
using System.Windows.Media;
using PortPane.Services;
using Serilog;

namespace PortPane.Views.Dialogs;

/// <summary>
/// Two-step confirmation dialog for resetting all PortPane settings.
/// Deletes settings.json via ISettingsService on confirmation.
/// Does NOT delete the license file or log folder.
/// </summary>
public partial class ResetConfirmDialog : Window
{
    private readonly ISettingsService _settings;

    public ResetConfirmDialog(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        // Populate runtime paths — never hardcoded
        SettingsPathBlock.Text = settings.SettingsFilePath;
        LicensePathBlock.Text  = settings.LicenseFilePath;
        LogPathBlock.Text      = settings.LogFolderPath;
    }

    private void ConfirmCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool isChecked = ConfirmCheckBox.IsChecked == true;
        ResetBtn.IsEnabled = isChecked;

        if (isChecked)
        {
            StatusNote.Text       = "Ready — click Reset Settings to proceed";
            StatusNote.Foreground = (Brush)FindResource("RadioBadgeBrush");
        }
        else
        {
            StatusNote.Text       = "Check the box above to enable Reset Settings";
            StatusNote.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmCheckBox.IsChecked != true) return;

        Log.Information("Settings reset by user. Restarting.");
        _settings.DeleteSettingsFile();

        DialogResult = true;
        Close();
    }
}
