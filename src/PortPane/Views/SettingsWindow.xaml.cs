using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PortPane.Services;
using PortPane.ViewModels;
using PortPane.Views.Dialogs;

namespace PortPane.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly IServiceProvider  _sp;

    public SettingsWindow(SettingsViewModel viewModel, ISettingsService settings, IServiceProvider sp)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
        _sp = sp;

        _vm.RequestResetDialog += OnRequestResetDialog;
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────────

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.OkCommand.Execute(null);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.CancelCommand.Execute(null);
        DialogResult = false;
    }

    // ── License file drag-drop ────────────────────────────────────────────────

    private void LicenseKeyBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string file = files[0];
            if (Path.GetExtension(file).Equals(".portpane", StringComparison.OrdinalIgnoreCase))
                _ = _vm.LicenseDropFile(file);
        }
    }

    // ── Telemetry data viewer ─────────────────────────────────────────────────

    private void ViewTelemetryData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var viewer = _sp.GetRequiredService<TelemetryDataViewer>();
            viewer.Owner = this;
            viewer.ShowDialog();
        }
        catch { /* non-fatal if viewer not available */ }
    }

    // ── Reset dialog orchestration ────────────────────────────────────────────

    private void OnRequestResetDialog(object? sender, EventArgs e)
    {
        var confirmDlg = _sp.GetRequiredService<ResetConfirmDialog>();
        confirmDlg.Owner = this;
        bool confirmed = confirmDlg.ShowDialog() == true;

        if (!confirmed) return;

        // Settings file was deleted inside ResetConfirmDialog; now show relaunch choice
        var completeDlg = _sp.GetRequiredService<ResetCompleteDialog>();
        completeDlg.Owner = this;
        completeDlg.ShowDialog();
        // ResetCompleteDialog handles Relaunch or Shutdown internally
    }
}
