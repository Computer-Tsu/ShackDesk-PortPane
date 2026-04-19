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
using PortPane.Views.Dialogs;
using Serilog;
using Velopack;

namespace PortPane;

public partial class App : Application
{
    private const string MutexName = "Global\\PortPane_ShackDesk_SingleInstance";
    private Mutex? _mutex;
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

        // ── Build expiry check (alpha/beta only) ──────────────────────────────
        if (ChannelInfo.BuildExpiryDays > 0 && !string.IsNullOrEmpty(BrandingInfo.BuildDate)
            && DateTimeOffset.TryParse(BrandingInfo.BuildDate, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var buildDate))
        {
            var expiry = buildDate.AddDays(ChannelInfo.BuildExpiryDays);
            if (DateTimeOffset.UtcNow > expiry)
            {
                MessageBox.Show(
                    $"This {BrandingInfo.FullVersion} build expired on {expiry:yyyy-MM-dd}.\n\n" +
                    $"Download the latest version at:\n{BrandingInfo.RepoURL}/releases",
                    $"{BrandingInfo.AppName} — Build Expired",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }
        }

        // ── Reset flag (wipes all user data and relaunches fresh) ─────────────
        if (e.Args.Contains("--reset", StringComparer.OrdinalIgnoreCase))
        {
            bool isPortable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt"));
            string dataDir = isPortable
                ? Path.Combine(AppContext.BaseDirectory, "PortPane-Data")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    BrandingInfo.SuiteName, BrandingInfo.AppName);
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
            Shutdown();
            return;
        }

        VelopackApp.Build().Run();

        // ── Bootstrap logging (before DI so DI errors are captured) ──────────
        InitLogging(isPortable: File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt")));

        // ── Global unhandled exception handlers ───────────────────────────────
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        Log.Information("{AppName} {Version} starting. Fingerprint: {FP}",
            BrandingInfo.AppName, BrandingInfo.FullVersion, Attribution.Fingerprint);

        // ── Dependency injection ──────────────────────────────────────────────
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Reconfigure logging now that SettingsService knows the correct path
        ReconfigureLogging(_serviceProvider.GetRequiredService<ISettingsService>());

        base.OnStartup(e);

        // ── First run dialog ──────────────────────────────────────────────────
        // ShutdownMode must be OnExplicitShutdown while the dialog is open;
        // with the default OnLastWindowClose, closing the dialog before MainWindow
        // is visible causes WPF to shut down the application immediately.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
        bool wasFirstRun = !settingsSvc.Current.FirstRunComplete;
        if (wasFirstRun)
        {
            _serviceProvider.GetRequiredService<FirstRunDialog>().ShowDialog();
        }

        // ── Main window ───────────────────────────────────────────────────────
        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // ── Background: telemetry — app launch ───────────────────────────────
        _ = Task.Run(async () =>
        {
            var telemetry = _serviceProvider.GetRequiredService<ITelemetryService>();
            await telemetry.ReportEventAsync("app_start", new Dictionary<string, object>
            {
                ["channel"]      = ChannelInfo.Channel.ToString(),
                ["is_portable"]  = settingsSvc.IsPortableMode,
                ["is_first_run"] = wasFirstRun
            });
        });

        // ── Background: update check ──────────────────────────────────────────
        _ = Task.Run(async () =>
        {
            var updateSvc = _serviceProvider.GetRequiredService<IUpdateService>();
            var info = await updateSvc.CheckForUpdateAsync();
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
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IComPortService, ComPortService>();
        services.AddSingleton<IHotplugService, HotplugService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IPuttyService, PuttyService>();

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
        services.AddTransient<ResetConfirmDialog>();
        services.AddTransient<ResetCompleteDialog>();
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
        var logConfig = new LoggerConfiguration();
        logConfig = ChannelInfo.VerboseLogging
            ? logConfig.MinimumLevel.Debug()
            : logConfig.MinimumLevel.Information();
        Log.Logger = logConfig
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
