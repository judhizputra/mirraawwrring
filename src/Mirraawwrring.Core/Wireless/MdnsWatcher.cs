using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Wireless;

/// <summary>
/// Polling <c>adb mdns services</c> tiap 5 detik untuk HP yang Wireless debugging-nya aktif di jaringan (§5.2).
/// Hanya layanan <c>_adb-tls-connect._tcp</c> yang dilaporkan. Event dari thread pool.
/// </summary>
public sealed class MdnsWatcher : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    private readonly IAdbClient _adb;
    private readonly ILogger<MdnsWatcher> _logger;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private IReadOnlyList<MdnsService> _services = [];
    private bool _lastFailed;

    public MdnsWatcher(IAdbClient adb, ILogger<MdnsWatcher> logger, TimeProvider? timeProvider = null, TimeSpan? interval = null)
    {
        _adb = adb;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _interval = interval ?? DefaultInterval;
    }

    public IReadOnlyList<MdnsService> Services => Volatile.Read(ref _services);

    public bool IsRunning => _loop is { IsCompleted: false };

    public event Action<IReadOnlyList<MdnsService>>? ServicesChanged;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
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

    public async Task<IReadOnlyList<MdnsService>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<MdnsService> services;
            try
            {
                var all = await _adb.GetMdnsServicesAsync(cancellationToken).ConfigureAwait(false);
                services = all.Where(s => s.IsConnectService).ToArray();
                _lastFailed = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!_lastFailed)
                {
                    _logger.LogDebug(ex, "adb mdns services gagal");
                }

                _lastFailed = true;
                return Services;
            }

            if (!Services.Select(s => (s.Name, s.Address)).OrderBy(k => k.Name).SequenceEqual(services.Select(s => (s.Name, s.Address)).OrderBy(k => k.Name)))
            {
                Volatile.Write(ref _services, services);
                _logger.LogDebug("Layanan mDNS berubah: {Count}", services.Count);
                ServicesChanged?.Invoke(services);
            }

            return services;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(_interval, _time);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
