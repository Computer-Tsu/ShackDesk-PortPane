using System.Windows;
using PortPane.Services;

namespace PortPane.Views;

public partial class TelemetryDataViewer : Window
{
    private readonly ITelemetryService _telemetry;

    public TelemetryDataViewer(ITelemetryService telemetry)
    {
        InitializeComponent();
        _telemetry = telemetry;
        Refresh();
    }

    private void Refresh()
    {
        var reports = _telemetry.GetPendingReports();
        ReportList.ItemsSource = reports;
        EmptyLabel.Visibility  = reports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReportList.Visibility  = reports.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var reports = _telemetry.GetPendingReports();
        if (reports.Count == 0) { MessageBox.Show("No data to send."); return; }

        // Re-send all pending reports
        foreach (var r in reports)
            await _telemetry.ReportEventAsync(r.EventName);

        _telemetry.ClearPendingReports();
        Refresh();
        MessageBox.Show("Report sent. Thank you for helping improve PortPane.",
            BrandingInfo.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _telemetry.ClearPendingReports();
        Refresh();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
