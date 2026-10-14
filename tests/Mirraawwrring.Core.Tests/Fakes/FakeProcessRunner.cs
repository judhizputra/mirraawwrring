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

    public IRunningProcess Start(ProcessRequest request, Action<ProcessOutputLine>? onOutput = null) =>
        throw new NotSupportedException("FakeProcessRunner.Start belum dibutuhkan di fase ini.");

    public ProcessRequest Last => Requests[^1];

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
