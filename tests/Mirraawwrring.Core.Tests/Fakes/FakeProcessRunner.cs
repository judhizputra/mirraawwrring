using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests;

/// <summary>IProcessRunner palsu: mencatat setiap request dan menjawab sesuai aturan yang didaftarkan.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(Func<ProcessRequest, bool> Match, Func<ProcessRequest, ProcessResult> Respond)> _rules = [];

    public List<ProcessRequest> Requests { get; } = [];

    public ProcessResult DefaultResult { get; set; } = ProcessResult.Ok();

    /// <summary>Jawab request yang argumennya diawali <paramref name="argsPrefix"/> (dibandingkan utuh per elemen).</summary>
    public FakeProcessRunner When(string[] argsPrefix, ProcessResult result)
    {
        _rules.Add((r => StartsWith(r.Arguments, argsPrefix), _ => result));
        return this;
    }

    public FakeProcessRunner When(Func<ProcessRequest, bool> match, Func<ProcessRequest, ProcessResult> respond)
    {
        _rules.Add((match, respond));
        return this;
    }

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        foreach (var (match, respond) in _rules)
        {
            if (match(request))
            {
                return Task.FromResult(respond(request));
            }
        }

        return Task.FromResult(DefaultResult);
    }

    public List<(ProcessRequest Request, FakeRunningProcess Process)> Started { get; } = [];

    private int _nextPid = 4000;

    public IRunningProcess Start(ProcessRequest request, Action<ProcessOutputLine>? onOutput = null)
    {
        Requests.Add(request);
        var process = new FakeRunningProcess(_nextPid++, onOutput);
        Started.Add((request, process));
        return process;
    }

    public ProcessRequest Last => Requests[^1];

    /// <summary>Proses palsu yang berjalan lama: test mengirim baris output dan mengakhirinya secara manual.</summary>
    public sealed class FakeRunningProcess(int pid, Action<ProcessOutputLine>? onOutput) : IRunningProcess
    {
        private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId { get; } = pid;

        public bool HasExited => _exit.Task.IsCompleted;

        public bool KillCalled { get; private set; }

        public void EmitStdout(string line) => onOutput?.Invoke(new ProcessOutputLine(false, line));

        public void EmitStderr(string line) => onOutput?.Invoke(new ProcessOutputLine(true, line));

        public void Exit(int code) => _exit.TrySetResult(code);

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => _exit.Task.WaitAsync(cancellationToken);

        public void Kill()
        {
            KillCalled = true;
            _exit.TrySetResult(-1);
        }

        public void Dispose() { }
    }

    private static bool StartsWith(IReadOnlyList<string> args, string[] prefix)
    {
        if (args.Count < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (!string.Equals(args[i], prefix[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
