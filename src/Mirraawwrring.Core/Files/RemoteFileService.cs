using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Files;

/// <summary>Kemajuan transfer: per file plus total byte (§5.9). Di dalam satu file besar progres tidak diketahui (indeterminate).</summary>
public sealed record TransferProgress(int FileIndex, int FileCount, string CurrentName, long DoneBytes, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? Math.Clamp((double)DoneBytes / TotalBytes, 0, 1) : FileCount > 0 ? (double)FileIndex / FileCount : 0;
}

/// <summary>Hasil transfer banyak file: yang berhasil, yang gagal (nama → pesan adb), dan byte yang dipindahkan.</summary>
public sealed record TransferResult(int Succeeded, IReadOnlyList<(string Name, string Message)> Failures, long Bytes, bool Cancelled)
{
    public bool AllSucceeded => Failures.Count == 0 && !Cancelled;
}

/// <summary>Listing gagal (folder tidak ada / tidak boleh dibaca).</summary>
public sealed class RemoteFileException(string message) : Exception(message);

/// <summary>
/// File browser HP (§5.9): listing lewat <c>find … -exec stat</c>, pull/push lewat <c>adb pull/push</c>, folder baru (<c>mkdir -p</c>),
/// ganti nama (<c>mv</c>), hapus (<c>rm -r</c>; konfirmasi dilakukan UI, §9). Semua perintah shell lewat <see cref="IAdbClient.ShellAsync"/>.
/// </summary>
public sealed class RemoteFileService(IAdbClient adb, IAppPaths paths, ILogger<RemoteFileService> logger)
{
    public static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan TransferTimeout = TimeSpan.FromHours(2);

    /// <summary>Folder tujuan "Simpan ke PC": dari settings atau <c>Downloads\Mirraawwrring</c>.</summary>
    public string DefaultPullDirectory => paths.DownloadsDirectory;

    public async Task<IReadOnlyList<RemoteEntry>> ListAsync(string serial, string directory, CancellationToken ct = default)
    {
        var dir = RemotePath.Normalize(directory);
        var result = await adb.ShellAsync(serial,
            ["find", dir, "-maxdepth", "1", "-mindepth", "1", "-exec", "stat", "-c", RemoteListingParser.StatFormat, "{}", "+"],
            ListTimeout, ct).ConfigureAwait(false);
        var listing = RemoteListingParser.Parse(result.StandardOutput);
        if (listing.Entries.Count == 0 && (!result.Succeeded || result.StandardError.Trim().Length > 0))
        {
            var message = result.CombinedOutput.Length > 0 ? result.CombinedOutput : $"find exit {result.ExitCode}";
            if (result.TimedOut)
            {
                message = "timeout";
            }

            throw new RemoteFileException(message);
        }

        foreach (var error in listing.Errors)
        {
            logger.LogDebug("Listing {Dir}: baris diabaikan: {Line}", dir, error);
        }

        return listing.Entries;
    }

    /// <summary><c>adb -s serial pull &lt;remote&gt; &lt;localDir&gt;</c> satu per satu; folder ditarik rekursif.</summary>
    public async Task<TransferResult> PullAsync(string serial, IReadOnlyList<RemoteEntry> entries, string localDirectory, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(localDirectory);
        var total = entries.Sum(e => e.Size);
        long done = 0;
        var failures = new List<(string, string)>();
        var ok = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                return new TransferResult(ok, failures, done, Cancelled: true);
            }

            var entry = entries[i];
            progress?.Report(new TransferProgress(i, entries.Count, entry.Name, done, total));
            var result = await adb.RunAsync(["-s", serial, "pull", entry.Path, localDirectory], TransferTimeout, cancellationToken: ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                ok++;
                done += entry.Size;
            }
            else
            {
                failures.Add((entry.Name, FirstMeaningfulLine(result)));
                logger.LogWarning("Pull {Path} gagal: {Message}", entry.Path, result.CombinedOutput);
            }
        }

        progress?.Report(new TransferProgress(entries.Count, entries.Count, string.Empty, done, total));
        return new TransferResult(ok, failures, done, Cancelled: false);
    }

    /// <summary><c>adb -s serial push &lt;local&gt; &lt;remoteDir&gt;/</c> satu per satu.</summary>
    public async Task<TransferResult> PushAsync(string serial, IReadOnlyList<string> localPaths, string remoteDirectory, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        var dir = RemotePath.Normalize(remoteDirectory);
        var sizes = localPaths.Select(LocalSize).ToArray();
        var total = sizes.Sum();
        long done = 0;
        var failures = new List<(string, string)>();
        var ok = 0;
        for (var i = 0; i < localPaths.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                return new TransferResult(ok, failures, done, Cancelled: true);
            }

            var name = Path.GetFileName(localPaths[i]);
            progress?.Report(new TransferProgress(i, localPaths.Count, name, done, total));
            var result = await adb.RunAsync(["-s", serial, "push", localPaths[i], dir + "/"], TransferTimeout, cancellationToken: ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                ok++;
                done += sizes[i];
            }
            else
            {
                failures.Add((name, FirstMeaningfulLine(result)));
                logger.LogWarning("Push {Path} gagal: {Message}", localPaths[i], result.CombinedOutput);
            }
        }

        progress?.Report(new TransferProgress(localPaths.Count, localPaths.Count, string.Empty, done, total));
        return new TransferResult(ok, failures, done, Cancelled: false);
    }

    public Task<ProcessResult> CreateDirectoryAsync(string serial, string parent, string name, CancellationToken ct = default)
    {
        EnsureValidName(name);
        return adb.ShellAsync(serial, ["mkdir", "-p", RemotePath.Combine(RemotePath.Normalize(parent), name)], cancellationToken: ct);
    }

    public Task<ProcessResult> RenameAsync(string serial, string path, string newName, CancellationToken ct = default)
    {
        EnsureValidName(newName);
        var parent = RemotePath.ParentOf(path) ?? "/";
        return adb.ShellAsync(serial, ["mv", path, RemotePath.Combine(parent, newName)], cancellationToken: ct);
    }

    /// <summary><c>rm -r</c>. Pemanggil WAJIB sudah meminta konfirmasi pengguna (PROMPT.md §9).</summary>
    public Task<ProcessResult> DeleteAsync(string serial, IReadOnlyList<string> remotePaths, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(remotePaths.Count);
        var args = new List<string> { "rm", "-r" };
        args.AddRange(remotePaths);
        return adb.ShellAsync(serial, args, TimeSpan.FromMinutes(5), ct);
    }

    private static void EnsureValidName(string name)
    {
        if (!RemotePath.IsValidName(name))
        {
            throw new ArgumentException("Nama tidak valid.", nameof(name));
        }
    }

    private static long LocalSize(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }

            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string FirstMeaningfulLine(ProcessResult result)
    {
        if (result.TimedOut)
        {
            return "timeout";
        }

        var line = result.CombinedOutput.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0 && !l.StartsWith('['));
        return line ?? $"exit {result.ExitCode}";
    }
}
