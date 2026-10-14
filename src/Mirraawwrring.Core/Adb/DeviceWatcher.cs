using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Adb;

/// <summary>
/// Polling <c>adb devices -l</c> tiap 2 detik dan mengeluarkan <see cref="DevicesChanged"/> hanya saat daftarnya berubah (§5.2).
/// Event dipanggil dari thread pool; UI harus marshal ke dispatcher.
/// </summary>
public sealed class DeviceWatcher : IAsyncDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly IAdbClient _adb;
    private readonly ILogger<DeviceWatcher> _logger;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private IReadOnlyList<Device> _devices = [];
    private bool _lastPollFailed;

    public DeviceWatcher(IAdbClient adb, ILogger<DeviceWatcher> logger, TimeProvider? timeProvider = null, TimeSpan? interval = null)
    {
        _adb = adb;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _interval = interval ?? DefaultInterval;
    }

    public IReadOnlyList<Device> Devices => Volatile.Read(ref _devices);

    public bool IsRunning => _loop is { IsCompleted: false };

    public event Action<IReadOnlyList<Device>>? DevicesChanged;

    /// <summary>Dipanggil saat polling gagal (adb tidak ada / tidak merespons); hanya sekali per rentetan kegagalan.</summary>
    public event Action<Exception>? PollFailed;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _loopCts = new CancellationTokenSource();
        _loop = RunLoopAsync(_loopCts.Token);
    }

    public async Task StopAsync()
    {
        if (_loopCts is null || _loop is null)
        {
            return;
        }

        await _loopCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _loopCts.Dispose();
        _loopCts = null;
        _loop = null;
    }

    /// <summary>Poll sekarang juga (tombol Refresh). Aman dipanggil bersamaan dengan loop.</summary>
    public async Task<IReadOnlyList<Device>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _pollGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<Device> devices;
            try
            {
                devices = await _adb.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
                _lastPollFailed = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!_lastPollFailed)
                {
                    _logger.LogWarning(ex, "Gagal membaca daftar perangkat");
                    PollFailed?.Invoke(ex);
                }

                _lastPollFailed = true;
                return Devices;
            }

            if (!SameList(Devices, devices))
            {
                Volatile.Write(ref _devices, devices);
                _logger.LogDebug("Daftar perangkat berubah: {Count} perangkat", devices.Count);
                DevicesChanged?.Invoke(devices);
            }

            return devices;
        }
        finally
        {
            _pollGate.Release();
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

    /// <summary>Dua daftar dianggap sama jika serial, state, dan model tiap perangkat sama (urutan diabaikan).</summary>
    internal static bool SameList(IReadOnlyList<Device> a, IReadOnlyList<Device> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        static IEnumerable<(string, DeviceState, string?)> Keys(IReadOnlyList<Device> list) =>
            list.Select(d => (d.Serial, d.State, d.Model)).OrderBy(k => k.Serial, StringComparer.Ordinal);

        return Keys(a).SequenceEqual(Keys(b));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _pollGate.Dispose();
    }
}
