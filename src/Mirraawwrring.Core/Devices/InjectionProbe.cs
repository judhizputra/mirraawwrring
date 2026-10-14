using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Devices;

public enum InjectionStatus
{
    /// <summary>Belum diuji.</summary>
    Unknown,

    /// <summary><c>input keyevent 0</c> berjalan tanpa pesan INJECT_EVENTS.</summary>
    Allowed,

    /// <summary>HyperOS memblokir: butuh "USB debugging (Security settings)".</summary>
    Blocked,

    /// <summary>Perintah gagal karena alasan lain (device terputus, timeout).</summary>
    Error,
}

/// <summary>
/// Uji injeksi input sekali per sesi per device: <c>adb -s serial shell input keyevent 0</c> (§5.4).
/// KEYCODE_UNKNOWN (0) tidak melakukan apa pun di HP.
/// </summary>
public sealed class InjectionProbe(IAdbClient adb, ILogger<InjectionProbe> logger)
{
    public const string BlockedMarker = "INJECT_EVENTS";

    private readonly ConcurrentDictionary<string, InjectionStatus> _cache = new(StringComparer.Ordinal);

    public InjectionStatus GetCached(string serial) => _cache.GetValueOrDefault(serial, InjectionStatus.Unknown);

    /// <summary>Hasil dari cache kalau sudah pernah diuji; kalau belum, uji sekarang.</summary>
    public async Task<InjectionStatus> ProbeAsync(string serial, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(serial, out var cached) && cached is InjectionStatus.Allowed or InjectionStatus.Blocked)
        {
            return cached;
        }

        return await RecheckAsync(serial, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uji ulang (tombol "Periksa ulang"), mengabaikan cache.</summary>
    public async Task<InjectionStatus> RecheckAsync(string serial, CancellationToken cancellationToken = default)
    {
        var result = await adb.ShellAsync(serial, ["input", "keyevent", "0"], cancellationToken: cancellationToken).ConfigureAwait(false);
        var status = Classify(result.TimedOut, result.ExitCode, result.CombinedOutput);
        _cache[serial] = status;
        logger.LogInformation("Uji injeksi {Serial}: {Status}", serial, status);
        return status;
    }

    public void Forget(string serial) => _cache.TryRemove(serial, out _);

    /// <summary>Fungsi murni untuk test.</summary>
    public static InjectionStatus Classify(bool timedOut, int exitCode, string output)
    {
        if (output.Contains(BlockedMarker, StringComparison.Ordinal))
        {
            return InjectionStatus.Blocked;
        }

        if (timedOut || exitCode != 0)
        {
            return InjectionStatus.Error;
        }

        return InjectionStatus.Allowed;
    }
}
