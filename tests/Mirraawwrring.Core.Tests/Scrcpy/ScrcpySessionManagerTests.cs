using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Scrcpy;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Tests.Scrcpy;

public sealed class ScrcpySessionManagerTests
{
    private const string ScrcpyPath = @"C:\tools\scrcpy\scrcpy.exe";
    private const string AdbPath = @"C:\tools\scrcpy\adb.exe";
    private readonly FakeProcessRunner _runner = new();
    private readonly ScrcpySessionManager _manager;

    public ScrcpySessionManagerTests()
    {
        var registry = new ToolRegistry();
        registry.Update(new ToolLocation { ScrcpyPath = ScrcpyPath, AdbPath = AdbPath });
        var paths = new FakeAppPaths { RecordingsDirectory = Path.Combine(Path.GetTempPath(), "mirraawwrring-tests", "rec") };
        var builder = new ScrcpyArgumentBuilder(paths, new FakeTimeProvider(DateTimeOffset.Now));
        _manager = new ScrcpySessionManager(_runner, registry, builder, NullLoggerFactory.Instance);
    }

    [Fact]
    public void Start_RunsScrcpyWithAdbEnvAndNoConsole()
    {
        var session = _manager.Start(new ScrcpyOptions(), TestDevices.Usb(), "Xiaomi 2407FPN8EG");

        var (request, process) = Assert.Single(_runner.Started);
        Assert.Equal(ScrcpyPath, request.FileName);
        Assert.Equal(AdbPath, request.Environment!["ADB"]);
        Assert.Equal(@"C:\tools\scrcpy", request.WorkingDirectory);
        Assert.Contains("--serial=R58N12ABCDE", request.Arguments);
        Assert.Contains("--window-title=Mirraawwrring - Xiaomi 2407FPN8EG", request.Arguments);
        Assert.Equal(process.ProcessId, session.ProcessId);
        Assert.True(session.IsRunning);
        Assert.Equal("Xiaomi 2407FPN8EG", session.DisplayName);
        Assert.Single(_manager.Sessions);
        Assert.Same(session, _manager.Find("R58N12ABCDE"));
    }

    [Fact]
    public void Start_SameSerialTwice_Throws()
    {
        _manager.Start(new ScrcpyOptions(), TestDevices.Usb());
        Assert.Throws<InvalidOperationException>(() => _manager.Start(new ScrcpyOptions(), TestDevices.Usb()));
    }

    [Fact]
    public async Task Output_IsClassified_InjectionBlockedRaisedOnce()
    {
        var session = _manager.Start(new ScrcpyOptions(), TestDevices.Usb());
        var process = _runner.Started[0].Process;
        var blocked = 0;
        var lines = new List<ScrcpyLogLine>();
        session.InjectionBlockedDetected += _ => blocked++;
        session.LogLine += (_, l) => lines.Add(l);

        process.EmitStdout("scrcpy 4.1 <https://github.com/Genymobile/scrcpy>");
        process.EmitStdout("[server] INFO: Device: [Xiaomi] Xiaomi 2407FPN8EG (Android 16)");
        process.EmitStderr("[server] ERROR: Injecting input events requires the caller (or the source of the instrumentation, if any) to have the INJECT_EVENTS permission.");
        process.EmitStderr("[server] ERROR: Make sure you have enabled \"USB debugging (Security Settings)\" and then rebooted your device.");

        Assert.Equal(1, blocked);
        Assert.True(session.InjectionBlocked);
        Assert.Equal("4.1", session.ScrcpyVersion);
        Assert.Equal("[Xiaomi] Xiaomi 2407FPN8EG (Android 16)", session.DeviceInfo);
        Assert.Equal(4, lines.Count);

        var ended = new TaskCompletionSource<int>();
        session.Exited += (_, code) => ended.TrySetResult(code);
        process.Exit(0);
        Assert.Equal(0, await ended.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await Task.Delay(50);
        Assert.Empty(_manager.Sessions);
        Assert.Null(_manager.Find("R58N12ABCDE"));
    }

    [Fact]
    public async Task Stop_KillsProcess_AndSessionEndedRaised()
    {
        var ended = new TaskCompletionSource<ScrcpySession>();
        _manager.SessionEnded += s => ended.TrySetResult(s);
        var session = _manager.Start(new ScrcpyOptions(), TestDevices.Usb());

        session.Stop();

        Assert.True(_runner.Started[0].Process.KillCalled);
        Assert.Same(session, await ended.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(-1, session.ExitCode);
    }

    [Fact]
    public void HeadlessRecording_HasSeparateKey()
    {
        var mirror = _manager.Start(new ScrcpyOptions(), TestDevices.Usb());
        var record = _manager.StartHeadlessRecording(TestDevices.Usb());

        Assert.NotEqual(mirror.Serial, record.Serial);
        Assert.Contains("--no-window", _runner.Started[1].Request.Arguments);
        Assert.Equal(2, _manager.Sessions.Count);
    }

    [Fact]
    public async Task GetVersion_ParsesFirstLine()
    {
        _runner.When(["--version"], ProcessResult.Ok("scrcpy 4.1 <https://github.com/Genymobile/scrcpy>\n\nDependencies (compiled / linked):\n - SDL: 3.2.0\n"));

        Assert.Equal("4.1", await _manager.GetVersionAsync());
    }

    [Fact]
    public void NoScrcpy_ThrowsToolNotFound()
    {
        var manager = new ScrcpySessionManager(_runner, new ToolRegistry(),
            new ScrcpyArgumentBuilder(new FakeAppPaths(), TimeProvider.System), NullLoggerFactory.Instance);

        Assert.Throws<ToolNotFoundException>(() => manager.Start(new ScrcpyOptions(), TestDevices.Usb()));
    }
}
