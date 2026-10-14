using System.Management;
using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Usb;

/// <summary>Membaca Win32_PnPEntity lewat WMI (System.Management). Kerja berat dijalankan di thread pool.</summary>
public sealed class WmiUsbProbe(ILogger<WmiUsbProbe> logger) : IUsbProbe
{
    private const string Query = "SELECT DeviceID, Name, PNPClass, CompatibleID, HardwareID FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB\\\\%'";

    public Task<UsbProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                return UsbProbeParser.Parse(ReadEntries());
            }
            catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Pemindaian USB lewat WMI gagal");
                return UsbProbeResult.Empty;
            }
        }, cancellationToken);

    private static List<PnpEntry> ReadEntries()
    {
        var list = new List<PnpEntry>();
        using var searcher = new ManagementObjectSearcher(Query);
        using var results = searcher.Get();
        foreach (var obj in results)
        {
            using (obj)
            {
                var deviceId = obj["DeviceID"] as string;
                if (deviceId is null)
                {
                    continue;
                }

                list.Add(new PnpEntry(
                    deviceId,
                    obj["Name"] as string,
                    obj["PNPClass"] as string,
                    obj["CompatibleID"] as string[] ?? [],
                    obj["HardwareID"] as string[] ?? []));
            }
        }

        return list;
    }
}

/// <summary>
/// Memantau perubahan USB: <c>Win32_DeviceChangeEvent</c> lewat ManagementEventWatcher, plus polling 2 detik sebagai cadangan (§5.3).
/// Event <see cref="Changed"/> hanya saat hasil berubah; dipanggil dari thread pool.
/// </summary>
public sealed class UsbWatcher : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly IUsbProbe _probe;
    private readonly ILogger<UsbWatcher> _logger;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ManagementEventWatcher? _eventWatcher;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private UsbProbeResult _last = UsbProbeResult.Empty;

    public UsbWatcher(IUsbProbe probe, ILogger<UsbWatcher> logger, TimeSpan? interval = null)
    {
        _probe = probe;
        _logger = logger;
        _interval = interval ?? DefaultInterval;
    }

    public UsbProbeResult Current => Volatile.Read(ref _last);

    public bool IsRunning => _loop is { IsCompleted: false };

    public event Action<UsbProbeResult>? Changed;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = RunLoopAsync(_cts.Token);
        TryStartEventWatcher();
    }

    public async Task StopAsync()
    {
        try
        {
            _eventWatcher?.Stop();
            _eventWatcher?.Dispose();
        }
        catch (ManagementException)
        {
        }

        _eventWatcher = null;
        if (_cts is null || _loop is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public async Task<UsbProbeResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (!Same(Current, result))
            {
                Volatile.Write(ref _last, result);
                _logger.LogDebug("USB berubah: HP={Phone} adb={Adb} merek={Brand}", result.PhoneConnected, result.AdbInterfacePresent, result.Brand);
                Changed?.Invoke(result);
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void TryStartEventWatcher()
    {
        try
        {
            _eventWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WITHIN 1"));
            _eventWatcher.EventArrived += (_, _) => _ = RefreshSafeAsync();
            _eventWatcher.Start();
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Win32_DeviceChangeEvent tidak tersedia, memakai polling saja");
            _eventWatcher = null;
        }
    }

    private async Task RefreshSafeAsync()
    {
        try
        {
            await RefreshAsync(_cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool Same(UsbProbeResult a, UsbProbeResult b) =>
        a.Devices.Select(d => (d.DeviceId, d.IsAdbInterface, d.IsMtpOrPtp)).OrderBy(k => k.DeviceId)
            .SequenceEqual(b.Devices.Select(d => (d.DeviceId, d.IsAdbInterface, d.IsMtpOrPtp)).OrderBy(k => k.DeviceId));

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
