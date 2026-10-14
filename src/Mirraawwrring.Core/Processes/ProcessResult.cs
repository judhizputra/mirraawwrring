namespace Mirraawwrring.Core.Processes;

/// <summary>Hasil perintah eksternal yang sudah selesai.</summary>
public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    byte[]? StandardOutputBytes = null)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;

    /// <summary>stdout lalu stderr, dipisah baris baru, tanpa whitespace di ujung.</summary>
    public string CombinedOutput
    {
        get
        {
            var o = StandardOutput.Trim();
            var e = StandardError.Trim();
            if (o.Length == 0)
            {
                return e;
            }

            return e.Length == 0 ? o : o + Environment.NewLine + e;
        }
    }

    public static ProcessResult Ok(string stdout = "", string stderr = "") =>
        new(0, stdout, stderr, TimeSpan.Zero, TimedOut: false);

    public static ProcessResult Fail(int exitCode, string stdout = "", string stderr = "") =>
        new(exitCode, stdout, stderr, TimeSpan.Zero, TimedOut: false);

    public static ProcessResult Timeout(string stdout = "", string stderr = "") =>
        new(-1, stdout, stderr, TimeSpan.Zero, TimedOut: true);
}
