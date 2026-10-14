using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Interop;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Scrcpy;

/// <summary>
/// Satu proses scrcpy yang sedang berjalan untuk satu perangkat (§5.6). Dibuat oleh <see cref="ScrcpySessionManager"/>.
/// Event dipanggil dari thread pool.
/// </summary>
public sealed class ScrcpySession : IDisposable
{
    public static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(10);

    private readonly IRunningProcess _process;
    private readonly ILogger _logger;
    private readonly Lazy<Task<nint>> _window;
    private int _injectionBlockedFlag;

    internal ScrcpySession(string serial, string displayName, ScrcpyLaunchPlan plan, Func<Action<ProcessOutputLine>, IRunningProcess> start, ILogger logger)
    {
        Serial = serial;
        DisplayName = displayName;
        Plan = plan;
        _logger = logger;
        _process = start(OnOutput);
        StartedAt = DateTimeOffset.Now;
        _window = new Lazy<Task<nint>>(() => WindowFinder.WaitForMainWindowAsync(ProcessId, WindowTimeout, () => _process.HasExited));
        _ = WatchExitAsync();
    }

    public string Serial { get; }

    /// <summary>Nama untuk UI (model atau serial).</summary>
    public string DisplayName { get; }

    public ScrcpyLaunchPlan Plan { get; }

    /// <summary>True untuk proses rekam tanpa jendela (<c>--no-window</c>); tidak mendapat toolbar.</summary>
    public bool IsHeadless => Plan.Arguments.Contains("--no-window");

    public DateTimeOffset StartedAt { get; }

    public int ProcessId => _process.ProcessId;

    public bool IsRunning => !_process.HasExited;

    public int? ExitCode { get; private set; }

    /// <summary>True setelah scrcpy melaporkan INJECT_EVENTS (HyperOS Security settings).</summary>
    public bool InjectionBlocked => Volatile.Read(ref _injectionBlockedFlag) == 1;

    /// <summary>Versi scrcpy dari baris pertama log, kalau sudah terbaca.</summary>
    public string? ScrcpyVersion { get; private set; }

    /// <summary>Info device dari <c>[server] INFO: Device: …</c>.</summary>
    public string? DeviceInfo { get; private set; }

    public event Action<ScrcpySession, ScrcpyLogLine>? LogLine;

    public event Action<ScrcpySession>? InjectionBlockedDetected;

    public event Action<ScrcpySession, int>? Exited;

    /// <summary>HWND jendela scrcpy; menunggu maksimal 10 detik. 0 kalau tidak muncul (mis. --no-window atau gagal).</summary>
    public Task<nint> GetWindowHandleAsync() => _window.Value;

    public void Stop()
    {
        if (IsRunning)
        {
            _logger.LogInformation("Menghentikan scrcpy untuk {Serial} (PID {Pid})", Serial, ProcessId);
            _process.Kill();
        }
    }

    private void OnOutput(ProcessOutputLine output)
    {
        var line = ScrcpyLogClassifier.Classify(output.Text);
        var level = line.Level switch
        {
            ScrcpyLogLevel.Error => LogLevel.Error,
            ScrcpyLogLevel.Warning => LogLevel.Warning,
            ScrcpyLogLevel.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };
        _logger.Log(level, "{Line}", line.Raw);

        switch (line.Kind)
        {
            case ScrcpyLogKind.Version:
                ScrcpyVersion = line.Detail;
                break;
            case ScrcpyLogKind.DeviceInfo:
                DeviceInfo = line.Detail;
                break;
            case ScrcpyLogKind.InjectionBlocked:
                if (Interlocked.Exchange(ref _injectionBlockedFlag, 1) == 0)
                {
                    InjectionBlockedDetected?.Invoke(this);
                }

                break;
        }

        LogLine?.Invoke(this, line);
    }

    private async Task WatchExitAsync()
    {
        int code;
        try
        {
            code = await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            code = -1;
        }

        ExitCode = code;
        if (code != 0)
        {
            _logger.LogWarning("scrcpy untuk {Serial} berhenti dengan kode {Code}", Serial, code);
        }
        else
        {
            _logger.LogInformation("scrcpy untuk {Serial} selesai", Serial);
        }

        Exited?.Invoke(this, code);
    }

    public void Dispose() => _process.Dispose();
}
