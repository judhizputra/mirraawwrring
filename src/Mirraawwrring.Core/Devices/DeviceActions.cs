using System.Globalization;
using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Devices;

/// <summary>Hasil screenshot: file PNG di <see cref="IAppPaths.ScreenshotsDirectory"/> plus byte-nya untuk clipboard.</summary>
public sealed record ScreenshotResult(bool Success, string? FilePath, byte[]? Png, string Message);

/// <summary>Kombinasi tombol yang dikirim ke jendela scrcpy (bukan perintah adb). MOD scrcpy = Alt kiri (default <c>--shortcut-mod</c>).</summary>
public sealed record ScrcpyKeyChord(bool Control, bool Alt, bool Shift, char Key, string Display);

/// <summary>
/// SATU-SATUNYA tempat perintah tombol toolbar (PROMPT.md §5.8). Semua <c>adb shell</c> lewat <see cref="IAdbClient.ShellAsync"/>
/// (escaping POSIX di <see cref="ShellQuoting"/>). Kolom "butuh injeksi" ada di <see cref="RequiresInjection"/>;
/// aksi yang dikirim sebagai tombol keyboard ke jendela scrcpy ada di <see cref="Chords"/>.
/// </summary>
public sealed class DeviceActions(IAdbClient adb, IAppPaths paths, TimeProvider time, ILogger<DeviceActions> logger)
{
    public const int KeyHome = 3;
    public const int KeyBack = 4;
    public const int KeyVolumeUp = 24;
    public const int KeyVolumeDown = 25;
    public const int KeyPower = 26;
    public const int KeyAppSwitch = 187;

    public const string ScreenshotFilePrefix = "screenshot_";
    public static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ScreenshotTimeout = TimeSpan.FromSeconds(20);

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Aksi yang memakai <c>input keyevent</c> dan karena itu diblokir HyperOS tanpa "USB debugging (Security settings)".</summary>
    public static IReadOnlySet<DeviceAction> RequiresInjection { get; } = new HashSet<DeviceAction>
    {
        DeviceAction.Power, DeviceAction.VolumeUp, DeviceAction.VolumeDown, DeviceAction.Back, DeviceAction.Home, DeviceAction.Recents,
        DeviceAction.SendClipboard,
    };

    /// <summary>Tombol yang dikirim ke jendela scrcpy lewat SendInput (lihat bagian Shortcuts di reference/scrcpy-4.1-help.txt).</summary>
    public static class Chords
    {
        /// <summary>Ctrl+V: scrcpy menyalin clipboard PC ke HP lalu menyuntik PASTE.</summary>
        public static readonly ScrcpyKeyChord Paste = new(Control: true, Alt: false, Shift: false, 'V', "Ctrl+V");

        /// <summary>MOD+o: layar HP dimatikan, mirroring tetap jalan.</summary>
        public static readonly ScrcpyKeyChord ScreenOff = new(Control: false, Alt: true, Shift: false, 'O', "Alt+O");

        /// <summary>MOD+Shift+o: layar HP dinyalakan.</summary>
        public static readonly ScrcpyKeyChord ScreenOn = new(Control: false, Alt: true, Shift: true, 'O', "Alt+Shift+O");
    }

    public Task<ProcessResult> KeyEventAsync(string serial, int keyCode, CancellationToken cancellationToken = default) =>
        adb.ShellAsync(serial, ["input", "keyevent", keyCode.ToString(CultureInfo.InvariantCulture)], cancellationToken: cancellationToken);

    public Task<ProcessResult> PowerAsync(string serial, CancellationToken ct = default) => KeyEventAsync(serial, KeyPower, ct);

    public Task<ProcessResult> BackAsync(string serial, CancellationToken ct = default) => KeyEventAsync(serial, KeyBack, ct);

    public Task<ProcessResult> HomeAsync(string serial, CancellationToken ct = default) => KeyEventAsync(serial, KeyHome, ct);

    public Task<ProcessResult> RecentsAsync(string serial, CancellationToken ct = default) => KeyEventAsync(serial, KeyAppSwitch, ct);

    /// <summary>
    /// Volume: <c>input keyevent 24/25</c>; jika injeksi diblokir, cadangan <c>cmd media_session volume --adj raise|lower</c>
    /// (belum diverifikasi di semua perangkat, lihat docs/manual-test.md).
    /// </summary>
    public Task<ProcessResult> VolumeAsync(string serial, bool up, bool useFallback, CancellationToken ct = default)
    {
        if (!useFallback)
        {
            return KeyEventAsync(serial, up ? KeyVolumeUp : KeyVolumeDown, ct);
        }

        return adb.ShellAsync(serial, ["cmd", "media_session", "volume", "--adj", up ? "raise" : "lower"], cancellationToken: ct);
    }

    /// <summary>Rotasi sekarang (0..3) dari <c>settings get system user_rotation</c>; null jika tidak terbaca.</summary>
    public async Task<int?> GetRotationAsync(string serial, CancellationToken ct = default)
    {
        var result = await adb.ShellAsync(serial, ["settings", "get", "system", "user_rotation"], cancellationToken: ct).ConfigureAwait(false);
        return result.Succeeded && int.TryParse(result.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) && r is >= 0 and <= 3
            ? r
            : null;
    }

    /// <summary>Putar 90° searah jarum jam: matikan rotasi otomatis lalu set <c>user_rotation</c>. Tidak butuh injeksi.</summary>
    public async Task<ProcessResult> RotateAsync(string serial, CancellationToken ct = default)
    {
        var current = await GetRotationAsync(serial, ct).ConfigureAwait(false) ?? 0;
        return await SetRotationAsync(serial, (current + 1) % 4, ct).ConfigureAwait(false);
    }

    public async Task<ProcessResult> SetRotationAsync(string serial, int rotation, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rotation, 3);
        ArgumentOutOfRangeException.ThrowIfNegative(rotation);
        var off = await adb.ShellAsync(serial, ["settings", "put", "system", "accelerometer_rotation", "0"], cancellationToken: ct).ConfigureAwait(false);
        if (!off.Succeeded)
        {
            return off;
        }

        return await adb.ShellAsync(serial, ["settings", "put", "system", "user_rotation", rotation.ToString(CultureInfo.InvariantCulture)], cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary><c>cmd statusbar expand-notifications</c> / <c>collapse</c>. Tidak butuh injeksi.</summary>
    public Task<ProcessResult> SetNotificationsAsync(string serial, bool expand, CancellationToken ct = default) =>
        adb.ShellAsync(serial, ["cmd", "statusbar", expand ? "expand-notifications" : "collapse"], cancellationToken: ct);

    /// <summary><c>settings put system show_touches 1/0</c>.</summary>
    public Task<ProcessResult> SetShowTouchesAsync(string serial, bool on, CancellationToken ct = default) =>
        adb.ShellAsync(serial, ["settings", "put", "system", "show_touches", on ? "1" : "0"], cancellationToken: ct);

    public async Task<bool> GetShowTouchesAsync(string serial, CancellationToken ct = default)
    {
        var result = await adb.ShellAsync(serial, ["settings", "get", "system", "show_touches"], cancellationToken: ct).ConfigureAwait(false);
        return result.Succeeded && result.StandardOutput.Trim() == "1";
    }

    /// <summary>Nama file screenshot berikutnya: <c>Pictures\Mirraawwrring\screenshot_yyyyMMdd_HHmmss.png</c>.</summary>
    public string NewScreenshotFilePath()
    {
        var stamp = time.GetLocalNow().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(paths.ScreenshotsDirectory, $"{ScreenshotFilePrefix}{stamp}.png");
    }

    /// <summary><c>adb -s serial exec-out screencap -p</c> (stdout biner) → PNG. Tidak butuh injeksi.</summary>
    public async Task<ScreenshotResult> ScreenshotAsync(string serial, CancellationToken ct = default)
    {
        var result = await adb.RunAsync(["-s", serial, "exec-out", "screencap", "-p"], ScreenshotTimeout, binaryStdout: true, cancellationToken: ct).ConfigureAwait(false);
        var bytes = result.StandardOutputBytes;
        if (!result.Succeeded || bytes is null || bytes.Length < PngSignature.Length || !bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            var message = result.TimedOut ? "screencap timeout" : result.StandardError.Trim();
            if (message.Length == 0)
            {
                message = $"screencap exit {result.ExitCode}, {bytes?.Length ?? 0} byte";
            }

            logger.LogWarning("Screenshot {Serial} gagal: {Message}", serial, message);
            return new ScreenshotResult(false, null, null, message);
        }

        var path = NewScreenshotFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            path = UniquePath(path);
            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Screenshot tidak bisa disimpan ke {Path}", path);
            return new ScreenshotResult(false, null, bytes, ex.Message);
        }

        logger.LogInformation("Screenshot {Serial} disimpan: {Path} ({Bytes} byte)", serial, path, bytes.Length);
        return new ScreenshotResult(true, path, bytes, path);
    }

    /// <summary><c>adb -s serial install -r file.apk</c>; timeout 3 menit.</summary>
    public Task<ProcessResult> InstallApkAsync(string serial, string apkPath, CancellationToken ct = default) =>
        adb.RunAsync(["-s", serial, "install", "-r", apkPath], InstallTimeout, cancellationToken: ct);

    /// <summary>Teks "Salin info device": serial, pabrikan, model, versi Android, resolusi (<c>wm size</c>).</summary>
    public async Task<string> GetDeviceInfoTextAsync(string serial, CancellationToken ct = default)
    {
        var details = await adb.GetDeviceDetailsAsync(serial, ct).ConfigureAwait(false);
        var size = await adb.ShellAsync(serial, ["wm", "size"], cancellationToken: ct).ConfigureAwait(false);
        var lines = new List<string>
        {
            $"Serial: {serial}",
            $"Manufacturer: {details.Manufacturer ?? "-"}",
            $"Model: {details.Model ?? "-"}",
            $"Android: {details.AndroidVersion ?? "-"}",
        };
        if (size.Succeeded && size.StandardOutput.Trim().Length > 0)
        {
            lines.Add(size.StandardOutput.Trim().Replace("Physical size: ", "Display: ", StringComparison.Ordinal));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Kalau file sudah ada (dua screenshot dalam detik yang sama), tambahkan akhiran -2, -3, ….</summary>
    internal static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name}-{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}

/// <summary>Identitas tombol toolbar (untuk tabel butuh-injeksi dan log).</summary>
public enum DeviceAction
{
    Power,
    VolumeUp,
    VolumeDown,
    Rotate,
    Back,
    Home,
    Recents,
    Notifications,
    Screenshot,
    Record,
    Files,
    InstallApk,
    SendClipboard,
    More,
}
