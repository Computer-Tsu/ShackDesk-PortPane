using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PortPane.Services;
using PortPane.ViewModels;

namespace PortPane.Views;

// Presentation-only code. No business logic. All logic lives in ViewModels and Services.
public partial class MainWindow : Window
{
    private readonly MainViewModel    _vm;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer  _chromehideTimer;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm         = viewModel;
        _settings   = settings;

        _chromehideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _chromehideTimer.Tick += (_, _) =>
        {
            _vm.IsChromeVisible = false;
            _chromehideTimer.Stop();
        };

        // Restore window position
        var pos = settings.Current.WindowPosition;
        Left = pos.X;
        Top  = pos.Y;

        Closed    += (_, _) => PersistWindowPosition();
        KeyDown   += OnKeyDown;
    }

    // ── Window drag (drag strip and chrome area) ──────────────────────────────

    private void DragStrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    // ── Chrome reveal/hide ────────────────────────────────────────────────────

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RevealChrome();
    }

    private void RevealChrome()
    {
        _vm.IsChromeVisible = true;
        _chromehideTimer.Stop();
        _chromehideTimer.Start();
    }

    // ── Window control buttons ────────────────────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    // ── Menu handlers (presentation routing only) ─────────────────────────────

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = ((App)Application.Current)
            .GetService<SettingsWindow>();
        win?.ShowDialog();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
        => _settings.Save();

    private void Exit_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        // Build a plaintext summary of all detected devices
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== {BrandingInfo.FullName} — Device Report ===");
        sb.AppendLine($"Playback: {_vm.Audio.DefaultPlaybackName}");
        sb.AppendLine($"Capture:  {_vm.Audio.DefaultCaptureName}");
        sb.AppendLine("COM Ports:");
        foreach (var p in _vm.ComPorts.Ports)
            sb.AppendLine($"  {p.PortName}  {p.FriendlyName}");
        Clipboard.SetText(sb.ToString());
    }

    private void Scale_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item
            && double.TryParse(item.Tag?.ToString(), out double factor))
        {
            _vm.ScaleFactor = factor;
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dlg = ((App)Application.Current).GetService<AboutDialog>();
        dlg?.ShowDialog();
    }

    private void HelpContents_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(BrandingInfo.SupportURL) { UseShellExecute = true });

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            var updateSvc = ((App)Application.Current).GetService<IUpdateService>();
            if (updateSvc is null) return;
            var info = await updateSvc.CheckForUpdateAsync(force: true);
            if (info is not null)
                Dispatcher.Invoke(() => _vm.NotifyUpdate(info));
            else
                Dispatcher.Invoke(() => MessageBox.Show("PortPane is up to date.",
                    BrandingInfo.AppName, MessageBoxButton.OK, MessageBoxImage.Information));
        });
    }

    private void ViewTelemetry_Click(object sender, RoutedEventArgs e)
    {
        var dlg = ((App)Application.Current).GetService<TelemetryDataViewer>();
        dlg?.ShowDialog();
    }

    private async void ApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.HasUpdate is false) return;
        var updateSvc = ((App)Application.Current).GetService<IUpdateService>();
        if (updateSvc is null) return;
        try
        {
            var info = await updateSvc.CheckForUpdateAsync(force: true);
            if (info is not null) await updateSvc.ApplyUpdateAsync(info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update failed: {ex.Message}",
                BrandingInfo.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { HelpContents_Click(sender, e); e.Handled = true; }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.OemComma: OpenSettings_Click(sender, e); e.Handled = true; break;
                case Key.S:        SaveSettings_Click(sender, e); e.Handled = true; break;
                case Key.T:        _vm.IsAlwaysOnTop = !_vm.IsAlwaysOnTop; e.Handled = true; break;
                case Key.P:        _vm.IsComPanelVisible = !_vm.IsComPanelVisible; e.Handled = true; break;
            }
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void PersistWindowPosition()
    {
        _settings.Current.WindowPosition.X = Left;
        _settings.Current.WindowPosition.Y = Top;
        _settings.Current.ScaleFactor      = _vm.ScaleFactor;
        _settings.Current.AlwaysOnTop      = _vm.IsAlwaysOnTop;
        _settings.Current.ComPanelVisible  = _vm.IsComPanelVisible;
        _settings.Save();
    }
}

// Extension helper for resolving services from App without referencing full DI container directly
file static class AppExtensions
{
    internal static T? GetService<T>(this App app) where T : class
        => ((System.IServiceProvider)app.GetType()
            .GetField("_serviceProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(app)!)
            ?.GetService(typeof(T)) as T;
}
