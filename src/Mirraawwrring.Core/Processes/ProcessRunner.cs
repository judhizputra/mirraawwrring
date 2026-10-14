using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Processes;

/// <summary>Implementasi nyata <see cref="IProcessRunner"/> di atas System.Diagnostics.Process.</summary>
public sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    private const int MaxLoggedOutputChars = 4000;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("> {Command}", request.ToLogString());
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process { StartInfo = CreateStartInfo(request) };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "Gagal menjalankan {File}", request.FileName);
            return new ProcessResult(-1, string.Empty, ex.Message, stopwatch.Elapsed, TimedOut: false);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.Timeout);

        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        Task<string>? stdoutTextTask = null;
        Task<byte[]>? stdoutBytesTask = null;
        if (request.BinaryStdout)
        {
            stdoutBytesTask = ReadAllBytesAsync(process.StandardOutput.BaseStream);
        }
        else
        {
            stdoutTextTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        }

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = !cancellationToken.IsCancellationRequested;
            TryKill(process);
            if (!timedOut)
            {
                throw;
            }
        }

        // Setelah proses keluar (atau dibunuh), pipa seharusnya tertutup. Beri batas agar handle yang diwarisi anak proses tidak menggantung.
        var readAll = Task.WhenAll(stderrTask, (Task?)stdoutTextTask ?? stdoutBytesTask!);
        var finished = await Task.WhenAny(readAll, Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None)).ConfigureAwait(false);
        var stdoutText = stdoutTextTask is { IsCompletedSuccessfully: true } ? stdoutTextTask.Result : string.Empty;
        var stdoutBytes = stdoutBytesTask is { IsCompletedSuccessfully: true } ? stdoutBytesTask.Result : null;
        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
        if (finished != readAll)
        {
            logger.LogWarning("Output {Command} tidak selesai dibaca (pipa masih dipegang proses lain)", request.ToLogString());
        }

        stopwatch.Stop();
        var exitCode = timedOut ? -1 : process.ExitCode;
        var result = new ProcessResult(exitCode, stdoutText, stderr, stopwatch.Elapsed, timedOut, stdoutBytes);
        LogResult(request, result);
        return result;
    }

    public IRunningProcess Start(ProcessRequest request, Action<ProcessOutputLine>? onOutput = null)
    {
        logger.LogInformation("> {Command}", request.ToLogString());
        var process = new Process { StartInfo = CreateStartInfo(request), EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                onOutput?.Invoke(new ProcessOutputLine(false, e.Data));
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                onOutput?.Invoke(new ProcessOutputLine(true, e.Data));
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        logger.LogDebug("Proses {Name} dimulai, PID {Pid}", Path.GetFileName(request.FileName), process.Id);
        return new RunningProcess(process, logger);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardErrorEncoding = Utf8NoBom,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
        };
        if (!request.BinaryStdout)
        {
            psi.StandardOutputEncoding = Utf8NoBom;
        }

        foreach (var arg in request.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        if (request.Environment is not null)
        {
            foreach (var (key, value) in request.Environment)
            {
                psi.Environment[key] = value;
            }
        }

        return psi;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private void LogResult(ProcessRequest request, ProcessResult result)
    {
        var output = result.StandardOutputBytes is not null
            ? $"<{result.StandardOutputBytes.Length} byte>" + (result.StandardError.Length > 0 ? Environment.NewLine + result.StandardError.Trim() : string.Empty)
            : result.CombinedOutput;
        if (output.Length > MaxLoggedOutputChars)
        {
            output = output[..MaxLoggedOutputChars] + Environment.NewLine + "(… dipotong)";
        }

        if (result.TimedOut)
        {
            logger.LogWarning("{Command}: timeout setelah {Seconds:0.0} s{Output}", request.ToLogString(), request.Timeout.TotalSeconds, Prefix(output));
        }
        else if (result.ExitCode != 0)
        {
            logger.LogWarning("{Command}: kode keluar {Code} ({Ms} ms){Output}", request.ToLogString(), result.ExitCode, (int)result.Duration.TotalMilliseconds, Prefix(output));
        }
        else if (output.Length > 0)
        {
            logger.LogInformation("{Output}", output);
        }

        static string Prefix(string output) => output.Length == 0 ? string.Empty : Environment.NewLine + output;
    }

    private sealed class RunningProcess(Process process, ILogger logger) : IRunningProcess
    {
        public int ProcessId { get; } = process.Id;

        public bool HasExited
        {
            get
            {
                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Proses PID {Pid} keluar dengan kode {Code}", ProcessId, process.ExitCode);
            return process.ExitCode;
        }

        public void Kill() => TryKill(process);

        public void Dispose() => process.Dispose();
    }
}
