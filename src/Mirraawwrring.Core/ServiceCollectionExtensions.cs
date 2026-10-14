using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.IO;
using Mirraawwrring.Core.Setup;
using Mirraawwrring.Core.Usb;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Scrcpy;
using Mirraawwrring.Core.Settings;
using Mirraawwrring.Core.Tools;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.Core;

/// <summary>Pendaftaran semua layanan Core ke DI. Dipanggil dari App.xaml.cs. Butuh IAppPaths, TimeProvider, dan logging sudah terdaftar.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMirraawwrringCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPaths, AppPaths>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IAdbClient, AdbClient>();
        services.AddSingleton<DeviceWatcher>();
        services.AddSingleton<ScrcpyArgumentBuilder>();
        services.AddSingleton<ScrcpySessionManager>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<WirelessService>();
        services.AddSingleton<MdnsWatcher>();

        services.AddSingleton<IUsbProbe, WmiUsbProbe>();
        services.AddSingleton<UsbWatcher>();
        services.AddSingleton<InjectionProbe>();
        services.AddSingleton<DeviceActions>();
        services.TryAddSingleton(_ => new HttpClient { DefaultRequestHeaders = { { "User-Agent", "Mirraawwrring" } } });
        services.AddSingleton<SetupService>();
        services.AddSingleton<VerificationService>();
        return services;
    }
}
