using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using PortPane.Services;

namespace PortPane.Views;

public partial class AboutDialog : Window
{
    private readonly ILicenseService _license;
    private readonly ISettingsService _settings;

    public AboutDialog(ILicenseService license, ISettingsService settings)
    {
        InitializeComponent();
        _license   = license;
        _settings  = settings;
        SupportIdText.Text = _settings.Current.InstallId;

        // Show licensee name for paid tiers
        if (license.Current.Tier != LicenseTier.Free)
        {
            LicenseeBlock.Text       = $"Licensed to: {license.Current.Licensee}";
            LicenseeBlock.Visibility = Visibility.Visible;
            SupportBlock.Visibility  = Visibility.Collapsed;
        }
    }

    // Easter egg: double-click logo → random animation
    private void Logo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            EasterEgg.Visibility = Visibility.Visible;
            EasterEgg.Trigger();
        }
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(BrandingInfo.RepoURL) { UseShellExecute = true });

    private void Support_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(BrandingInfo.SupportURL) { UseShellExecute = true });

    private void CopySupportId_Click(object sender, RoutedEventArgs e)
        => Clipboard.SetText(_settings.Current.InstallId);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
