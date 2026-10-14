namespace Mirraawwrring.Core.Tools;

/// <summary>Dari mana scrcpy/adb ditemukan, sesuai urutan pencarian §5.1.</summary>
public enum ToolSource
{
    /// <summary><c>&lt;folder exe&gt;\scrcpy\</c></summary>
    BundledFolder,

    /// <summary><c>&lt;folder exe&gt;\</c></summary>
    ExeFolder,

    /// <summary>Path kustom dari settings.</summary>
    CustomPath,

    /// <summary>Symlink winget di <c>%LOCALAPPDATA%\Microsoft\WinGet\Links</c>.</summary>
    Winget,

    /// <summary>Dari variabel PATH.</summary>
    Path,

    /// <summary>Folder yang sama dengan scrcpy.exe (khusus adb).</summary>
    ScrcpyFolder,

    /// <summary>Lokasi umum Android SDK platform-tools (khusus adb, hanya untuk peringatan).</summary>
    AndroidSdk,
}

/// <summary>Hasil pencarian tools. Bisa tidak lengkap; wizard setup menanganinya.</summary>
public sealed record ToolLocation
{
    public static readonly ToolLocation NotFound = new();

    /// <summary>Path scrcpy.exe yang sudah di-resolve dari symlink (kalau ada).</summary>
    public string? ScrcpyPath { get; init; }

    public ToolSource? ScrcpySource { get; init; }

    /// <summary>Path adb.exe yang dipakai aplikasi, diprioritaskan dari folder scrcpy supaya versinya cocok.</summary>
    public string? AdbPath { get; init; }

    public ToolSource? AdbSource { get; init; }

    /// <summary>adb lain yang terdeteksi (PATH, Android SDK) selain <see cref="AdbPath"/>; untuk peringatan bentrok versi.</summary>
    public IReadOnlyList<string> OtherAdbPaths { get; init; } = [];

    public bool HasScrcpy => ScrcpyPath is not null;

    public bool HasAdb => AdbPath is not null;

    public bool IsComplete => HasScrcpy && HasAdb;

    public string? ScrcpyDirectory => ScrcpyPath is null ? null : System.IO.Path.GetDirectoryName(ScrcpyPath);

    public string? ScrcpyServerPath => ScrcpyDirectory is null ? null : System.IO.Path.Combine(ScrcpyDirectory, "scrcpy-server");
}
