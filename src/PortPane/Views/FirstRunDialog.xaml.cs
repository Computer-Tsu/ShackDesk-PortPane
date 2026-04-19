using System.Diagnostics;
using System.Windows;
using PortPane.Services;

namespace PortPane.Views;

public partial class FirstRunDialog : Window
{
    private readonly ISettingsService  _settings;
    private readonly ITelemetryService _telemetry;

    public FirstRunDialog(ISettingsService settings, ITelemetryService telemetry)
    {
        InitializeComponent();
        _settings  = settings;
        _telemetry = telemetry;
        TelemetryCheckBox.IsChecked = _settings.Current.TelemetryEnabled;
    }

    private void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        _telemetry.IsEnabled           = TelemetryCheckBox.IsChecked == true;
        _settings.Current.TelemetryEnabled  = _telemetry.IsEnabled;
        _settings.Current.FirstRunComplete   = true;
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void License_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(BrandingInfo.RepoURL + "/blob/main/LICENSE-MIT.md")
               { UseShellExecute = true });

    private void Privacy_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(BrandingInfo.PrivacyURL)
               { UseShellExecute = true });
}
