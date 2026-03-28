using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PortPane.Logging;
using PortPane.Models;
using PortPane.Services;
using PortPane.ViewModels;
using PortPane.Views;
using Serilog;
using Velopack;

namespace PortPane;

public partial class App : Application
{
    private const string MutexName = "Global\\PortPane_ShackDesk_SingleInstance";
    private Mutex?           _mutex;
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Single instance ───────────────────────────────────────────────────
        _mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            NativeMethods.BringExistingInstanceToForeground();
            Shutdown();
            return;
        }

        VelopackApp.Build().Run();

        // ── Bootstrap logging (before DI so DI errors are captured) ──────────
        InitLogging(isPortable: File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt")));

        // ── Global unhandled exception handlers ───────────────────────────────
        DispatcherUnhandledException                  += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException    += OnDomainException;

        Log.Information("{AppName} {Version} starting. Fingerprint: {FP}",
            BrandingInfo.AppName, BrandingInfo.Version, Attribution.Fingerprint);

        // ── Dependency injection ──────────────────────────────────────────────
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Reconfigure logging now that SettingsService knows the correct path
        ReconfigureLogging(_serviceProvider.GetRequiredService<ISettingsService>());

        base.OnStartup(e);

        // ── First run dialog ──────────────────────────────────────────────────
        var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
        if (!settingsSvc.Current.FirstRunComplete)
        {
            _serviceProvider.GetRequiredService<FirstRunDialog>().ShowDialog();
        }

        // ── Main window ───────────────────────────────────────────────────────
        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();

        // ── Background: update check ──────────────────────────────────────────
        _ = Task.Run(async () =>
        {
            var updateSvc = _serviceProvider.GetRequiredService<IUpdateService>();
            var info      = await updateSvc.CheckForUpdateAsync();
            if (info is not null)
            {
                Dispatcher.Invoke(() =>
                {
                    var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
                    mainVm.NotifyUpdate(info);
                });
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _serviceProvider?.GetRequiredService<ISettingsService>().Save();
            _serviceProvider?.GetRequiredService<IHotplugService>().Stop();
            Log.Information("{AppName} shutting down", BrandingInfo.AppName);
        }
        finally
        {
            Log.CloseAndFlush();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }

    // ── Service registration ──────────────────────────────────────────────────

    private void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<HttpClient>(_ =>
            new HttpClient { Timeout = TimeSpan.FromSeconds(3) });

        // Settings first — other services depend on it
        services.AddSingleton<ISettingsService, SettingsService>();

        // USB device database — loaded from bundled Data directory
        services.AddSingleton(_ =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "usb_devices.json");
            return UsbDeviceDatabase.Load(path);
        });

        // Core services
        services.AddSingleton<IAudioService,    AudioService>();
        services.AddSingleton<IComPortService,  ComPortService>();
        services.AddSingleton<IHotplugService,  HotplugService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IUpdateService,   UpdateService>();
        services.AddSingleton<ILicenseService,  LicenseService>();
        services.AddSingleton<IPuttyService,    PuttyService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<AudioPanelViewModel>();
        services.AddSingleton<ComPortPanelViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows / dialogs
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<AboutDialog>();
        services.AddTransient<FirstRunDialog>();
        services.AddTransient<TelemetryDataViewer>();
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private static void InitLogging(bool isPortable)
    {
        string dir = isPortable
            ? Path.Combine(AppContext.BaseDirectory, "PortPane-Data", "logs")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BrandingInfo.SuiteName, BrandingInfo.AppName, "logs");
        SetupSerilog(dir);
    }

    private static void ReconfigureLogging(ISettingsService settings)
    {
        string dir = settings.IsPortableMode
            ? Path.Combine(AppContext.BaseDirectory, "PortPane-Data", "logs")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BrandingInfo.SuiteName, BrandingInfo.AppName, "logs");
        Log.CloseAndFlush();
        SetupSerilog(dir);
        Log.Debug("Logging active — dir={Dir} portable={Portable}", dir, settings.IsPortableMode);
    }

    private static void SetupSerilog(string logDir)
    {
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: Path.Combine(logDir, "portpane-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                hooks: new LogFileHeaderHooks())
            .CreateLogger();
    }

    // ── Error handling ────────────────────────────────────────────────────────

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleFatal(e.Exception);
        e.Handled = true;
        Shutdown(1);
    }

    private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled AppDomain exception");
        Log.CloseAndFlush();
    }

    private void HandleFatal(Exception ex)
    {
        Log.Fatal(ex, "Unhandled exception — application closing");

        try
        {
            var telemetry = _serviceProvider?.GetService<ITelemetryService>();
            if (telemetry?.IsEnabled == true)
                _ = telemetry.ReportCrashAsync(ex);
        }
        catch { /* telemetry must never prevent graceful shutdown */ }

        string logPath = _serviceProvider?.GetService<ISettingsService>()?.SettingsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BrandingInfo.SuiteName, BrandingInfo.AppName, "logs");

        MessageBox.Show(
            $"PortPane encountered an unexpected error and needs to close.\n\n" +
            $"A report has been saved to:\n{logPath}\n\n" +
            $"Please report this at:\n{BrandingInfo.SupportURL}",
            $"{BrandingInfo.AppName} — Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
