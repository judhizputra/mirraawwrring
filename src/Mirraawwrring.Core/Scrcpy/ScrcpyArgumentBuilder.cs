using System.Globalization;

namespace Mirraawwrring.Core.Scrcpy;

/// <summary>Argumen siap jalan plus info turunan yang dibutuhkan UI.</summary>
public sealed record ScrcpyLaunchPlan(IReadOnlyList<string> Arguments, string WindowTitle, string? RecordFilePath)
{
    public string ToCommandLine() => "scrcpy " + string.Join(' ', Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}

/// <summary>
/// SATU-SATUNYA tempat pemetaan pilihan UI → argumen scrcpy (§5.5). Nama flag diverifikasi terhadap
/// <c>scrcpy 4.1 --help</c> (lihat reference/scrcpy-4.1-help.txt).
///
/// Aturan konflik:
/// <list type="number">
/// <item>Mode Hemat data menang atas resolusi/bitrate/FPS manual (selalu 1024 px · 4M · 30 fps).</item>
/// <item>Mode Lihat saja (<c>--no-control</c>) membuat opsi Input UHID diabaikan (tidak ada input yang dikirim).</item>
/// <item>Mode Layar HP mati sudah menyertakan <c>--stay-awake</c>; opsi "Layar HP tetap menyala" tidak menduplikasinya.</item>
/// <item>Resolusi "Asli" dan FPS "Tanpa batas" = flag tidak dikirim; bitrate selalu dikirim.</item>
/// </list>
/// </summary>
public sealed class ScrcpyArgumentBuilder(IAppPaths paths, TimeProvider time)
{
    public const string WindowTitlePrefix = "Mirraawwrring - ";
    public const string RecordFilePrefix = "scrcpy_";
    public const string RecordFileExtension = ".mp4";

    /// <summary>
    /// Rekaman dari toolbar dihentikan dengan mematikan proses scrcpy (tidak ada jendela untuk ditutup), sehingga trailer MP4
    /// tidak pernah ditulis dan filenya tak bisa diputar. Matroska tetap bisa diputar walau terpotong, jadi rekaman headless memakai .mkv.
    /// </summary>
    public const string HeadlessRecordFileExtension = ".mkv";

    public const int DataSaverMaxSize = 1024;
    public const int DataSaverBitRateMbps = 4;
    public const int DataSaverMaxFps = 30;

    /// <summary>Bangun argumen untuk sesi mirroring interaktif.</summary>
    public ScrcpyLaunchPlan Build(ScrcpyOptions options, string serial, string? modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var title = WindowTitlePrefix + (string.IsNullOrWhiteSpace(modelName) ? serial : modelName.Trim());
        var args = new List<string>
        {
            $"--serial={serial}",
            $"--window-title={title}",
        };

        // Mode
        switch (options.Mode)
        {
            case MirrorMode.ViewOnly:
                args.Add("--no-control");
                break;
            case MirrorMode.ScreenOff:
                args.Add("--turn-screen-off");
                args.Add("--stay-awake");
                break;
            case MirrorMode.MirrorAndControl:
            case MirrorMode.DataSaver:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Mode tidak dikenal");
        }

        // Kualitas (aturan 1 dan 4)
        var maxSize = options.Mode == MirrorMode.DataSaver ? DataSaverMaxSize : options.MaxSize;
        var bitRate = options.Mode == MirrorMode.DataSaver ? DataSaverBitRateMbps : options.VideoBitRateMbps;
        var maxFps = options.Mode == MirrorMode.DataSaver ? DataSaverMaxFps : options.MaxFps;
        if (maxSize is > 0)
        {
            args.Add($"--max-size={Invariant(maxSize.Value)}");
        }

        args.Add($"--video-bit-rate={Invariant(bitRate)}M");
        if (maxFps is > 0)
        {
            args.Add($"--max-fps={Invariant(maxFps.Value)}");
        }

        // Opsi
        if (!options.ForwardAudio)
        {
            args.Add("--no-audio");
        }

        if (options.UhidInput && options.Mode != MirrorMode.ViewOnly)
        {
            // Aturan 2. Catatan: --mouse=uhid menangkap mouse PC (mode relatif); LAlt/LSuper melepasnya.
            args.Add("--keyboard=uhid");
            args.Add("--mouse=uhid");
        }

        if (options.AlwaysOnTop)
        {
            args.Add("--always-on-top");
        }

        if (options.Fullscreen)
        {
            args.Add("--fullscreen");
        }

        if (options.StayAwake && options.Mode != MirrorMode.ScreenOff)
        {
            // Aturan 3.
            args.Add("--stay-awake");
        }

        string? recordPath = null;
        if (options.RecordToFile)
        {
            recordPath = NewRecordFilePath();
            args.Add($"--record={recordPath}");
        }

        return new ScrcpyLaunchPlan(args, title, recordPath);
    }

    /// <summary>Argumen untuk proses rekam layar tanpa jendela (tombol Rekam layar di toolbar, §5.8).</summary>
    public ScrcpyLaunchPlan BuildHeadlessRecording(string serial, string? modelName, bool withAudio = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        var title = WindowTitlePrefix + (string.IsNullOrWhiteSpace(modelName) ? serial : modelName.Trim()) + " (rekam)";
        var recordPath = NewRecordFilePath(HeadlessRecordFileExtension);
        var args = new List<string>
        {
            $"--serial={serial}",
            "--no-window",
            "--no-playback",
            "--no-control",
        };
        if (!withAudio)
        {
            args.Add("--no-audio");
        }

        args.Add($"--record={recordPath}");
        return new ScrcpyLaunchPlan(args, title, recordPath);
    }

    /// <summary><c>Videos\Mirraawwrring\scrcpy_yyyyMMdd_HHmmss.mp4</c> (atau ekstensi lain).</summary>
    public string NewRecordFilePath(string extension = RecordFileExtension)
    {
        var stamp = time.GetLocalNow().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(paths.RecordingsDirectory, $"{RecordFilePrefix}{stamp}{extension}");
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
