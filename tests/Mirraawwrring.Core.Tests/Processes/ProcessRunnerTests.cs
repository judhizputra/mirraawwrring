using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests.Processes;

/// <summary>Uji integrasi ringan dengan cmd.exe (tanpa adb).</summary>
public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new(NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task Run_CapturesStdoutStderrAndExitCode()
    {
        var result = await _runner.RunAsync(ProcessRequest.Create("cmd.exe", "/c", "echo halo & echo galat 1>&2 & exit 3"));

        Assert.False(result.TimedOut);
        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("halo", result.StandardOutput.Trim());
        Assert.Equal("galat", result.StandardError.Trim());
        Assert.Equal("halo" + Environment.NewLine + "galat", result.CombinedOutput);
    }

    [Fact]
    public async Task Run_TimeoutKillsProcess()
    {
        var request = new ProcessRequest
        {
            FileName = "cmd.exe",
            Arguments = ["/c", "ping -n 30 127.0.0.1 >nul"],
            Timeout = TimeSpan.FromMilliseconds(500),
        };

        var started = DateTime.UtcNow;
        var result = await _runner.RunAsync(request);

        Assert.True(result.TimedOut);
        Assert.False(result.Succeeded);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Run_CancellationThrows()
    {
        using var cts = new CancellationTokenSource(200);
        var request = ProcessRequest.Create("cmd.exe", "/c", "ping -n 30 127.0.0.1 >nul");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _runner.RunAsync(request, cts.Token));
    }

    [Fact]
    public async Task Run_MissingExecutable_ReturnsFailureInsteadOfThrowing()
    {
        var result = await _runner.RunAsync(ProcessRequest.Create(@"C:\tidak\ada\adb.exe", "devices"));

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.StandardError);
    }

    [Fact]
    public async Task Run_EnvironmentVariableIsPassed()
    {
        var request = new ProcessRequest
        {
            FileName = "cmd.exe",
            Arguments = ["/c", "echo %ADB%"],
            Environment = new Dictionary<string, string> { ["ADB"] = @"C:\x\adb.exe" },
        };

        var result = await _runner.RunAsync(request);

        Assert.Equal(@"C:\x\adb.exe", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Run_BinaryStdout()
    {
        var request = new ProcessRequest { FileName = "cmd.exe", Arguments = ["/c", "echo abc"], BinaryStdout = true };

        var result = await _runner.RunAsync(request);

        Assert.NotNull(result.StandardOutputBytes);
        Assert.Equal("abc", System.Text.Encoding.ASCII.GetString(result.StandardOutputBytes).Trim());
    }

    [Fact]
    public async Task Start_StreamsLinesAndWaitsForExit()
    {
        var lines = new System.Collections.Concurrent.ConcurrentBag<ProcessOutputLine>();
        using var running = _runner.Start(ProcessRequest.Create("cmd.exe", "/c", "echo satu & echo dua 1>&2"), lines.Add);

        var code = await running.WaitForExitAsync();
        await Task.Delay(100); // beri waktu callback async terakhir

        Assert.Equal(0, code);
        Assert.True(running.HasExited);
        Assert.Contains(lines, l => !l.IsError && l.Text.Trim() == "satu");
        Assert.Contains(lines, l => l.IsError && l.Text.Trim() == "dua");
    }

    [Fact]
    public void ToLogString_QuotesArgumentsWithSpaces()
    {
        var request = ProcessRequest.Create(@"C:\tools\adb.exe", "-s", "SER", "shell", "input text 'a b'");
        Assert.Equal("adb -s SER shell \"input text 'a b'\"", request.ToLogString());
    }
}
