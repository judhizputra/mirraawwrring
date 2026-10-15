using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Interop;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Setup;

public enum SetupStage
{
    Downloading,
    Verifying,
    Extracting,
    Unblocking,
    Done,
}

/// <summary>Progress unduhan/pemasangan untuk wizard.</summary>
public sealed record SetupProgress(SetupStage Stage, long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : null;
}

public sealed record FolderValidation(bool IsValid, IReadOnlyList<string> MissingFiles, string? ScrcpyPath, string? AdbPath);

/// <summary>Dilempar jika SHA-256 tidak cocok; file unduhan sudah dihapus.</summary>
public sealed class ChecksumMismatchException(string expected, string actual)
    : Exception($"SHA-256 tidak cocok. Diharapkan {expected}, didapat {actual}. File unduhan dihapus.")
{
    public string Expected { get; } = expected;
    public string Actual { get; } = actual;
}

/// <summary>Unduh &amp; pasang scrcpy (§5.7). Tidak pernah mengekstrak file yang hash-nya tidak cocok (§9).</summary>
public sealed class SetupService(IAppPaths paths, IProcessRunner runner, HttpClient http, ILogger<SetupService> logger)
{
    public static readonly string[] RequiredFiles = ["scrcpy.exe", "adb.exe", "scrcpy-server"];
    public const string WingetPackageId = "Genymobile.scrcpy";

    /// <summary>Manifest aktif: override di folder data (hasil pembaruan otomatis) kalau ada, kalau tidak manifest bawaan.</summary>
    public ScrcpyManifest LoadManifest()
    {
        if (File.Exists(paths.ScrcpyManifestOverrideFile))
        {
            try
            {
                return ScrcpyManifest.Load(paths.ScrcpyManifestOverrideFile);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException)
            {
                logger.LogWarning(ex, "Manifest override rusak, memakai manifest bawaan: {Path}", paths.ScrcpyManifestOverrideFile);
            }
        }

        return ScrcpyManifest.Load(paths.ScrcpyManifestFile);
    }

    /// <summary>Simpan manifest versi baru ke folder data setelah pembaruan otomatis berhasil.</summary>
    public void SaveManifestOverride(ScrcpyManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ScrcpyManifestOverrideFile)!);
        File.WriteAllText(paths.ScrcpyManifestOverrideFile, System.Text.Json.JsonSerializer.Serialize(manifest, ManifestJson));
    }

    private static readonly System.Text.Json.JsonSerializerOptions ManifestJson = new() { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    /// <summary>Cek koneksi internet dengan HEAD ke URL manifest (timeout 6 detik).</summary>
    public async Task<bool> HasInternetAsync(ScrcpyManifest manifest, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(6));
            using var request = new HttpRequestMessage(HttpMethod.Head, manifest.Url);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            return (int)response.StatusCode < 500;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return false;
        }
    }

    /// <summary>
    /// Unduh zip ke folder sementara, verifikasi SHA-256, ekstrak isi <c>folderInZip</c> ke <see cref="IAppPaths.ScrcpyBundleDirectory"/>,
    /// lalu hapus Zone.Identifier semua file. Mengembalikan folder tujuan.
    /// </summary>
    public async Task<string> DownloadAndInstallAsync(ScrcpyManifest manifest, IProgress<SetupProgress>? progress, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(paths.DataDirectory, "download");
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, manifest.FileName);

        try
        {
            await DownloadAsync(manifest.Url, zipPath, progress, cancellationToken).ConfigureAwait(false);
            progress?.Report(new SetupProgress(SetupStage.Verifying, 0, null));
            await VerifyChecksumAsync(zipPath, manifest.Sha256, cancellationToken).ConfigureAwait(false);
            progress?.Report(new SetupProgress(SetupStage.Extracting, 0, null));
            var target = paths.ScrcpyBundleDirectory;
            ExtractFolder(zipPath, manifest.FolderInZip, target);
            progress?.Report(new SetupProgress(SetupStage.Unblocking, 0, null));
            UnblockDirectory(target);
            progress?.Report(new SetupProgress(SetupStage.Done, 0, null));
            logger.LogInformation("scrcpy {Version} terpasang di {Dir}", manifest.Version, target);
            return target;
        }
        finally
        {
            TryDelete(zipPath);
        }
    }

    private async Task DownloadAsync(string url, string destination, IProgress<SetupProgress>? progress, CancellationToken cancellationToken)
    {
        logger.LogInformation("Mengunduh {Url}", url);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        progress?.Report(new SetupProgress(SetupStage.Downloading, 0, total));
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Report(new SetupProgress(SetupStage.Downloading, received, total));
        }
    }

    /// <summary>Hitung SHA-256 file; jika tidak cocok, hapus file dan lempar <see cref="ChecksumMismatchException"/>.</summary>
    public async Task VerifyChecksumAsync(string filePath, string expectedSha256, CancellationToken cancellationToken = default)
    {
        var actual = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(filePath);
            logger.LogError("SHA-256 tidak cocok untuk {File}: {Actual}", filePath, actual);
            throw new ChecksumMismatchException(expectedSha256.ToLowerInvariant(), actual);
        }

        logger.LogInformation("SHA-256 cocok: {Hash}", actual);
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Ekstrak entri di bawah <paramref name="folderInZip"/> (tanpa prefix foldernya) ke <paramref name="target"/>, menimpa file lama.</summary>
    public static void ExtractFolder(string zipPath, string folderInZip, string target)
    {
        var prefix = folderInZip.Trim('/', '\\') + "/";
        Directory.CreateDirectory(target);
        var targetFull = Path.GetFullPath(target + Path.DirectorySeparatorChar);
        using var zip = ZipFile.OpenRead(zipPath);
        var count = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.Length == prefix.Length)
            {
                continue;
            }

            var relative = name[prefix.Length..];
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Entri zip keluar dari folder tujuan: {entry.FullName}");
            }

            if (relative.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            count++;
        }

        if (count == 0)
        {
            throw new InvalidDataException($"Folder '{folderInZip}' tidak ada di dalam zip.");
        }
    }

    /// <summary>Hapus alternate data stream <c>Zone.Identifier</c> (tanda "file dari internet") dari semua file di folder.</summary>
    public static int UnblockDirectory(string directory)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (NativeMethods.DeleteFile(file + ":Zone.Identifier"))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Jalankan winget; output diteruskan baris per baris. Mengembalikan exit code.</summary>
    public async Task<int> InstallViaWingetAsync(Action<string> onOutputLine, CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest
        {
            FileName = "winget",
            Arguments = ["install", "--id", WingetPackageId, "-e", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity"],
        };
        using var process = runner.Start(request, line => onOutputLine(line.Text));
        using var registration = cancellationToken.Register(process.Kill);
        return await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Validasi folder pilihan pengguna: harus ada scrcpy.exe, adb.exe, scrcpy-server.</summary>
    public static FolderValidation ValidateFolder(string folder)
    {
        if (File.Exists(folder) && string.Equals(Path.GetFileName(folder), ToolLocator.ScrcpyExe, StringComparison.OrdinalIgnoreCase))
        {
            folder = Path.GetDirectoryName(folder)!;
        }

        var missing = RequiredFiles.Where(f => !File.Exists(Path.Combine(folder, f))).ToList();
        return new FolderValidation(
            missing.Count == 0,
            missing,
            missing.Count == 0 ? Path.Combine(folder, ToolLocator.ScrcpyExe) : null,
            missing.Count == 0 ? Path.Combine(folder, ToolLocator.AdbExe) : null);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
