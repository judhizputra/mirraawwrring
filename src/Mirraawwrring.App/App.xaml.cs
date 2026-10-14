using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.App.ViewModels;
using Mirraawwrring.App.Views;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Logging;
using Mirraawwrring.Core.Settings;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.App;

/// <summary>
/// Titik masuk aplikasi: membangun container DI, logging, lalu membuka jendela utama.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    public static new App Current => (App)Application.Current;

    /// <summary>Container DI. Hanya untuk kode yang tidak bisa menerima dependensi lewat konstruktor (mis. markup extension).</summary>
    public IServiceProvider Services => _services ?? throw new InvalidOperationException("Container DI belum dibangun.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        _logger = _services.GetRequiredService<ILogger<App>>();
        var paths = _services.GetRequiredService<IAppPaths>();
        _logger.LogInformation("Mirraawwrring {Version} dimulai. Data: {DataDir} ({Mode})",
            typeof(App).Assembly.GetName().Version, paths.DataDirectory, paths.IsPortable ? "portable" : "LocalAppData");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        LocateTools();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Mirraawwrring ditutup (kode {Code})", e.ApplicationExitCode);
        // DisposeAsync: DeviceWatcher hanya IAsyncDisposable; sesi scrcpy yang masih jalan dihentikan oleh ScrcpySessionManager.Dispose.
        _services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    /// <summary>Cari scrcpy/adb (§5.1) dan simpan ke registry. Path kustom dari settings menyusul di Fase 2.</summary>
    private void LocateTools()
    {
        var customPath = Services.GetRequiredService<ISettingsStore>().Current.CustomScrcpyPath;
        var location = Services.GetRequiredService<IToolLocator>().Locate(customPath);
        Services.GetRequiredService<IToolRegistry>().Update(location);
        if (location.IsComplete)
        {
            _logger?.LogInformation("scrcpy: {Scrcpy} ({ScrcpySource}); adb: {Adb} ({AdbSource})",
                location.ScrcpyPath, location.ScrcpySource, location.AdbPath, location.AdbSource);
        }
        else
        {
            _logger?.LogWarning("Tools belum lengkap. scrcpy: {Scrcpy}; adb: {Adb}", location.ScrcpyPath ?? "-", location.AdbPath ?? "-");
        }

        if (location.OtherAdbPaths.Count > 0)
        {
            _logger?.LogWarning("adb lain terdeteksi (potensi bentrok versi): {Others}", string.Join("; ", location.OtherAdbPaths));
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var paths = new AppPaths();
        var logBuffer = new LogBuffer();
        var fileWriter = new FileLogWriter(paths.LogsDirectory);

        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton(logBuffer);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new AppLoggerProvider(logBuffer, fileWriter));
        });

        services.AddMirraawwrringCore();

        // ViewModel dan View
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<HyperOsGuideWindow>();
        services.AddSingleton<Func<HyperOsGuideWindow>>(sp => sp.GetRequiredService<HyperOsGuideWindow>);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.Exception, "Exception tidak tertangani di UI thread");
        MessageBox.Show(e.Exception.Message, Strings.Get("Error_UnexpectedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.ExceptionObject as Exception, "Exception tidak tertangani (AppDomain)");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Exception Task tidak diamati");
        e.SetObserved();
    }
}
