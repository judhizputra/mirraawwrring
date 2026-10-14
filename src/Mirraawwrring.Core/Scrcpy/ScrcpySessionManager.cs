using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Scrcpy;

/// <summary>
/// Menjalankan dan melacak beberapa sesi scrcpy sekaligus (satu per serial). scrcpy dijalankan tanpa jendela konsol,
/// dengan env <c>ADB</c> menunjuk ke adb yang dipakai aplikasi supaya versinya sama (§5.1, §5.6).
/// </summary>
public sealed class ScrcpySessionManager(
    IProcessRunner runner,
    IToolRegistry tools,
    ScrcpyArgumentBuilder builder,
    ILoggerFactory loggerFactory) : IDisposable
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Scrcpy");
    private readonly object _gate = new();
    private readonly List<ScrcpySession> _sessions = [];

    public IReadOnlyList<ScrcpySession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _sessions.ToArray();
            }
        }
    }

    public event Action<ScrcpySession>? SessionStarted;

    public event Action<ScrcpySession>? SessionEnded;

    public ScrcpySession? Find(string serial)
    {
        lock (_gate)
        {
            return _sessions.FirstOrDefault(s => s.Serial == serial && s.IsRunning);
        }
    }

    /// <summary>Mulai mirroring interaktif. Melempar <see cref="InvalidOperationException"/> jika serial itu sudah punya sesi aktif.</summary>
    public ScrcpySession Start(ScrcpyOptions options, Device device, string? displayModel = null)
    {
        var model = displayModel ?? device.Model;
        var plan = builder.Build(options, device.Serial, model);
        return StartPlan(device.Serial, model ?? device.Serial, plan);
    }

    /// <summary>Mulai proses rekam layar tanpa jendela (toolbar, Fase 5).</summary>
    public ScrcpySession StartHeadlessRecording(Device device, bool withAudio = true)
    {
        var plan = builder.BuildHeadlessRecording(device.Serial, device.Model, withAudio);
        return StartPlan(device.Serial + "#record", (device.Model ?? device.Serial) + " (rekam)", plan);
    }

    private ScrcpySession StartPlan(string key, string displayName, ScrcpyLaunchPlan plan)
    {
        var scrcpy = tools.RequireScrcpy();
        var adb = tools.RequireAdb();
        if (plan.RecordFilePath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.RecordFilePath)!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Folder rekaman tidak bisa dibuat: {Dir}", Path.GetDirectoryName(plan.RecordFilePath));
            }
        }

        lock (_gate)
        {
            if (_sessions.Any(s => s.Serial == key && s.IsRunning))
            {
                throw new InvalidOperationException($"Sesi scrcpy untuk {key} sudah berjalan.");
            }

            var request = new ProcessRequest
            {
                FileName = scrcpy,
                Arguments = plan.Arguments,
                WorkingDirectory = Path.GetDirectoryName(scrcpy),
                Environment = new Dictionary<string, string> { ["ADB"] = adb },
            };

            var session = new ScrcpySession(key, displayName, plan, onOutput => runner.Start(request, onOutput), _logger);
            session.Exited += OnSessionExited;
            _sessions.Add(session);
            _logger.LogInformation("Sesi scrcpy dimulai untuk {Name} (PID {Pid})", displayName, session.ProcessId);
            SessionStarted?.Invoke(session);
            return session;
        }
    }

    private void OnSessionExited(ScrcpySession session, int code)
    {
        lock (_gate)
        {
            _sessions.Remove(session);
        }

        SessionEnded?.Invoke(session);
        session.Dispose();
    }

    /// <summary><c>scrcpy --version</c>; mengembalikan mis. "4.1" atau null jika gagal.</summary>
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest
        {
            FileName = tools.RequireScrcpy(),
            Arguments = ["--version"],
            Environment = new Dictionary<string, string> { ["ADB"] = tools.Current.AdbPath ?? string.Empty },
        };
        var result = await runner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        foreach (var line in result.StandardOutput.Split('\n'))
        {
            var classified = ScrcpyLogClassifier.Classify(line);
            if (classified.Kind == ScrcpyLogKind.Version)
            {
                return classified.Detail;
            }
        }

        return null;
    }

    public void StopAll()
    {
        foreach (var session in Sessions)
        {
            session.Stop();
        }
    }

    public void Dispose() => StopAll();
}
