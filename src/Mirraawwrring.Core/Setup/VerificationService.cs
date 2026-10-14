using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.IO;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Setup;

public enum CheckStatus
{
    Pending,
    Running,
    Ok,
    Warning,
    Failed,
    Skipped,
}

public enum CheckGroup
{
    Scrcpy,
    Adb,
    Phone,
}

/// <summary>Pemeriksaan yang gagal bisa punya aksi perbaikan (tombol di layar Setup3b-Gagal).</summary>
public enum FixAction
{
    None,

    /// <summary>Unduh ulang scrcpy (kembali ke langkah Pasang dengan opsi unduh).</summary>
    Redownload,

    /// <summary>Jalankan <c>"&lt;adb lain&gt;" kill-server</c> lalu periksa ulang.</summary>
    KillOtherAdb,

    /// <summary>Buka panduan USB debugging (layar Setup3c-HP).</summary>
    EnableUsbDebugging,

    /// <summary>Buka panduan Security settings HyperOS.</summary>
    SecuritySettings,
}

public enum CheckId
{
    ScrcpyFiles,
    ScrcpyRuns,
    AdbRuns,
    AdbServer,
    OtherAdb,
    PhoneAuthorized,
    PhoneInjection,
}

public sealed record CheckResult(CheckId Id, CheckGroup Group, CheckStatus Status, string Detail, FixAction Fix = FixAction.None, string? FixArgument = null);

/// <summary>Langkah Verifikasi wizard (§6.1): grup scrcpy, ADB, HP. Hasil dilaporkan satu per satu lewat callback supaya UI bisa live.</summary>
public sealed class VerificationService(
    IToolRegistry tools,
    IFileSystem fs,
    IProcessRunner runner,
    IAdbClient adb,
    InjectionProbe injection,
    ILogger<VerificationService> logger)
{
    public static readonly string[] ScrcpyRequiredFiles = ["scrcpy.exe", "scrcpy-server", "adb.exe", "SDL3.dll", "avcodec-62.dll", "avformat-62.dll", "avutil-60.dll", "swresample-6.dll", "libusb-1.0.dll"];

    public static readonly CheckId[] AllChecks = [CheckId.ScrcpyFiles, CheckId.ScrcpyRuns, CheckId.AdbRuns, CheckId.AdbServer, CheckId.OtherAdb, CheckId.PhoneAuthorized, CheckId.PhoneInjection];

    /// <summary>Jalankan semua pemeriksaan berurutan. <paramref name="skipPhone"/> melewati grup HP.</summary>
    public async Task<IReadOnlyList<CheckResult>> RunAsync(bool skipPhone, Action<CheckResult> report, CancellationToken cancellationToken = default)
    {
        var results = new List<CheckResult>();
        var location = tools.Current;

        async Task Run(CheckId id, CheckGroup group, Func<Task<CheckResult>> check)
        {
            report(new CheckResult(id, group, CheckStatus.Running, string.Empty));
            CheckResult result;
            try
            {
                result = await check().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Pemeriksaan {Id} melempar exception", id);
                result = new CheckResult(id, group, CheckStatus.Failed, ex.Message);
            }

            results.Add(result);
            report(result);
        }

        await Run(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, () => Task.FromResult(CheckScrcpyFiles(location))).ConfigureAwait(false);
        await Run(CheckId.ScrcpyRuns, CheckGroup.Scrcpy, () => CheckScrcpyRunsAsync(location, cancellationToken)).ConfigureAwait(false);
        await Run(CheckId.AdbRuns, CheckGroup.Adb, () => CheckAdbRunsAsync(location, cancellationToken)).ConfigureAwait(false);
        await Run(CheckId.AdbServer, CheckGroup.Adb, () => CheckAdbServerAsync(location, cancellationToken)).ConfigureAwait(false);
        await Run(CheckId.OtherAdb, CheckGroup.Adb, () => Task.FromResult(CheckOtherAdb(location))).ConfigureAwait(false);

        if (skipPhone)
        {
            foreach (var id in new[] { CheckId.PhoneAuthorized, CheckId.PhoneInjection })
            {
                var skipped = new CheckResult(id, CheckGroup.Phone, CheckStatus.Skipped, string.Empty);
                results.Add(skipped);
                report(skipped);
            }

            return results;
        }

        string? serial = null;
        await Run(CheckId.PhoneAuthorized, CheckGroup.Phone, async () =>
        {
            var r = await CheckPhoneAuthorizedAsync(location, cancellationToken).ConfigureAwait(false);
            serial = r.FixArgument;
            return r with { FixArgument = null, Fix = r.Fix };
        }).ConfigureAwait(false);

        if (serial is null)
        {
            var skipped = new CheckResult(CheckId.PhoneInjection, CheckGroup.Phone, CheckStatus.Skipped, string.Empty);
            results.Add(skipped);
            report(skipped);
            return results;
        }

        await Run(CheckId.PhoneInjection, CheckGroup.Phone, () => CheckInjectionAsync(serial!, cancellationToken)).ConfigureAwait(false);
        return results;
    }

    public CheckResult CheckScrcpyFiles(ToolLocation location)
    {
        if (location.ScrcpyDirectory is null)
        {
            return new CheckResult(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, CheckStatus.Failed, "scrcpy.exe tidak ditemukan", FixAction.Redownload);
        }

        var missing = ScrcpyRequiredFiles.Where(f => !fs.FileExists(Path.Combine(location.ScrcpyDirectory, f))).ToList();
        if (missing.Count == 0)
        {
            return new CheckResult(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, CheckStatus.Ok, $"{ScrcpyRequiredFiles.Length} file");
        }

        // scrcpy dari winget/paket lain bisa punya versi DLL berbeda; yang wajib hanya exe, server, adb.
        var essential = missing.Where(m => SetupService.RequiredFiles.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
        return essential.Count == 0
            ? new CheckResult(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, CheckStatus.Ok, $"versi DLL berbeda: {string.Join(", ", missing)}")
            : new CheckResult(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, CheckStatus.Failed, $"hilang: {string.Join(", ", essential)}", FixAction.Redownload);
    }

    private async Task<CheckResult> CheckScrcpyRunsAsync(ToolLocation location, CancellationToken ct)
    {
        if (location.ScrcpyPath is null)
        {
            return new CheckResult(CheckId.ScrcpyRuns, CheckGroup.Scrcpy, CheckStatus.Failed, "scrcpy.exe tidak ditemukan", FixAction.Redownload);
        }

        var result = await runner.RunAsync(new ProcessRequest
        {
            FileName = location.ScrcpyPath,
            Arguments = ["--version"],
            Environment = new Dictionary<string, string> { ["ADB"] = location.AdbPath ?? string.Empty },
        }, ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return new CheckResult(CheckId.ScrcpyRuns, CheckGroup.Scrcpy, CheckStatus.Failed, result.CombinedOutput.Length > 0 ? result.CombinedOutput : "scrcpy.exe tidak bisa dijalankan", FixAction.Redownload);
        }

        var first = result.StandardOutput.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        var version = Scrcpy.ScrcpyLogClassifier.Classify(first);
        return new CheckResult(CheckId.ScrcpyRuns, CheckGroup.Scrcpy, CheckStatus.Ok, version.Kind == Scrcpy.ScrcpyLogKind.Version ? $"scrcpy {version.Detail}" : first);
    }

    private async Task<CheckResult> CheckAdbRunsAsync(ToolLocation location, CancellationToken ct)
    {
        if (location.AdbPath is null)
        {
            return new CheckResult(CheckId.AdbRuns, CheckGroup.Adb, CheckStatus.Failed, "adb.exe tidak ditemukan", FixAction.Redownload);
        }

        var result = await adb.VersionAsync(ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return new CheckResult(CheckId.AdbRuns, CheckGroup.Adb, CheckStatus.Failed, result.CombinedOutput.Length > 0 ? result.CombinedOutput : "adb.exe tidak bisa dijalankan", FixAction.Redownload);
        }

        var first = result.StandardOutput.Split('\n').FirstOrDefault()?.Trim() ?? "OK";
        return new CheckResult(CheckId.AdbRuns, CheckGroup.Adb, CheckStatus.Ok, first.Replace("Android Debug Bridge version", "adb", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<CheckResult> CheckAdbServerAsync(ToolLocation location, CancellationToken ct)
    {
        if (location.AdbPath is null)
        {
            return new CheckResult(CheckId.AdbServer, CheckGroup.Adb, CheckStatus.Skipped, string.Empty);
        }

        var result = await adb.StartServerAsync(ct).ConfigureAwait(false);
        if (result.Success)
        {
            return new CheckResult(CheckId.AdbServer, CheckGroup.Adb, result.VersionMismatch ? CheckStatus.Warning : CheckStatus.Ok,
                result.VersionMismatch ? "server adb lain diganti; port 5037" : "port 5037");
        }

        var other = location.OtherAdbPaths.FirstOrDefault();
        return new CheckResult(CheckId.AdbServer, CheckGroup.Adb, CheckStatus.Failed,
            result.Message.Length > 0 ? result.Message : "adb start-server gagal",
            other is not null ? FixAction.KillOtherAdb : FixAction.None, other);
    }

    public static CheckResult CheckOtherAdb(ToolLocation location) =>
        location.OtherAdbPaths.Count == 0
            ? new CheckResult(CheckId.OtherAdb, CheckGroup.Adb, CheckStatus.Ok, string.Empty)
            : new CheckResult(CheckId.OtherAdb, CheckGroup.Adb, CheckStatus.Warning, string.Join("; ", location.OtherAdbPaths));

    private async Task<CheckResult> CheckPhoneAuthorizedAsync(ToolLocation location, CancellationToken ct)
    {
        if (location.AdbPath is null)
        {
            return new CheckResult(CheckId.PhoneAuthorized, CheckGroup.Phone, CheckStatus.Skipped, string.Empty);
        }

        var devices = await adb.GetDevicesAsync(ct).ConfigureAwait(false);
        var ready = devices.FirstOrDefault(d => d.IsReady);
        if (ready is not null)
        {
            return new CheckResult(CheckId.PhoneAuthorized, CheckGroup.Phone, CheckStatus.Ok, ready.DisplayName, FixAction.None, ready.Serial);
        }

        if (devices.Any(d => d.State == DeviceState.Unauthorized))
        {
            return new CheckResult(CheckId.PhoneAuthorized, CheckGroup.Phone, CheckStatus.Failed, "belum diizinkan: tekan Izinkan di layar HP", FixAction.EnableUsbDebugging);
        }

        return new CheckResult(CheckId.PhoneAuthorized, CheckGroup.Phone, CheckStatus.Failed, "tidak ada HP terdeteksi", FixAction.EnableUsbDebugging);
    }

    private async Task<CheckResult> CheckInjectionAsync(string serial, CancellationToken ct)
    {
        var status = await injection.RecheckAsync(serial, ct).ConfigureAwait(false);
        return status switch
        {
            InjectionStatus.Allowed => new CheckResult(CheckId.PhoneInjection, CheckGroup.Phone, CheckStatus.Ok, "kontrol diizinkan"),
            InjectionStatus.Blocked => new CheckResult(CheckId.PhoneInjection, CheckGroup.Phone, CheckStatus.Warning, "diblokir HyperOS: aktifkan USB debugging (Security settings) atau pakai Input UHID", FixAction.SecuritySettings),
            _ => new CheckResult(CheckId.PhoneInjection, CheckGroup.Phone, CheckStatus.Warning, "tidak bisa diuji"),
        };
    }

    /// <summary>Aksi perbaikan "Hentikan &amp; ulangi": <c>"&lt;adb lain&gt;" kill-server</c>. Hanya dari tombol yang jelas (§9).</summary>
    public async Task<ProcessResult> KillOtherAdbAsync(string otherAdbPath, CancellationToken ct = default)
    {
        logger.LogInformation("Menghentikan server adb lain: {Path}", otherAdbPath);
        return await runner.RunAsync(ProcessRequest.Create(otherAdbPath, "kill-server"), ct).ConfigureAwait(false);
    }
}
