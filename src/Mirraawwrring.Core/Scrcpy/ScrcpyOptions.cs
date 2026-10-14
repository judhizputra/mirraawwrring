namespace Mirraawwrring.Core.Scrcpy;

/// <summary>Empat mode di panel "Mode" jendela utama.</summary>
public enum MirrorMode
{
    /// <summary>Tampilkan layar dan kendalikan dengan mouse &amp; keyboard (default scrcpy).</summary>
    MirrorAndControl,

    /// <summary>Hanya menampilkan layar (<c>--no-control</c>).</summary>
    ViewOnly,

    /// <summary>Layar HP dimatikan, tetap tampil di PC (<c>--turn-screen-off --stay-awake</c>).</summary>
    ScreenOff,

    /// <summary>1024 px · 4 Mbps · 30 fps untuk Wi-Fi lambat.</summary>
    DataSaver,
}

/// <summary>Pilihan pengguna di jendela utama. Dipetakan ke argumen hanya oleh <see cref="ScrcpyArgumentBuilder"/>.</summary>
public sealed record ScrcpyOptions
{
    public MirrorMode Mode { get; init; } = MirrorMode.MirrorAndControl;

    /// <summary>Resolusi maks (px sisi terpanjang). null = Asli (tidak dikirim).</summary>
    public int? MaxSize { get; init; }

    /// <summary>Bitrate video dalam Mbps.</summary>
    public int VideoBitRateMbps { get; init; } = 8;

    /// <summary>FPS maks. null = tanpa batas.</summary>
    public int? MaxFps { get; init; } = 60;

    /// <summary>Teruskan audio HP ke PC. false = <c>--no-audio</c>.</summary>
    public bool ForwardAudio { get; init; } = true;

    /// <summary>Input UHID (<c>--keyboard=uhid --mouse=uhid</c>), berguna saat injeksi SDK diblokir HyperOS.</summary>
    public bool UhidInput { get; init; }

    public bool AlwaysOnTop { get; init; }

    public bool Fullscreen { get; init; }

    public bool StayAwake { get; init; }

    /// <summary>Rekam ke file di Videos\Mirraawwrring.</summary>
    public bool RecordToFile { get; init; }
}
