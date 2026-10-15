namespace Mirraawwrring.Core;

/// <summary>
/// Satu-satunya tempat yang tahu lokasi folder dan file aplikasi (prinsip §4.7).
/// </summary>
public interface IAppPaths
{
    /// <summary>Folder tempat exe berada (untuk single-file publish: folder exe-nya).</summary>
    string ExeDirectory { get; }

    /// <summary>Folder data (settings.json, logs). Portable di <c>&lt;exe&gt;\data</c> kalau bisa ditulis, kalau tidak di LocalAppData.</summary>
    string DataDirectory { get; }

    /// <summary>True jika data disimpan di samping exe (mode portable).</summary>
    bool IsPortable { get; }

    string LogsDirectory { get; }
    string SettingsFile { get; }

    /// <summary><c>&lt;exe&gt;\scrcpy</c>, lokasi hasil unduhan otomatis SetupService.</summary>
    string ScrcpyBundleDirectory { get; }

    /// <summary><c>&lt;exe&gt;\config\scrcpy-manifest.json</c>.</summary>
    string ScrcpyManifestFile { get; }

    /// <summary><c>&lt;data&gt;\scrcpy-manifest.json</c>: ditulis setelah pembaruan scrcpy otomatis, diutamakan atas manifest bawaan.</summary>
    string ScrcpyManifestOverrideFile { get; }

    /// <summary><c>%LOCALAPPDATA%\Microsoft\WinGet\Links\scrcpy.exe</c> (symlink buatan winget).</summary>
    string WingetScrcpyLink { get; }

    /// <summary>Lokasi umum adb dari Android SDK, dicek untuk peringatan bentrok versi.</summary>
    string AndroidSdkAdb { get; }

    /// <summary>Videos\Mirraawwrring.</summary>
    string RecordingsDirectory { get; }

    /// <summary>Pictures\Mirraawwrring.</summary>
    string ScreenshotsDirectory { get; }

    /// <summary>Downloads\Mirraawwrring.</summary>
    string DownloadsDirectory { get; }
}

public sealed class AppPaths : IAppPaths
{
    public const string AppName = "Mirraawwrring";

    private readonly Lazy<(string Dir, bool Portable)> _data;

    public AppPaths() : this(AppContext.BaseDirectory) { }

    /// <param name="exeDirectory">Folder exe; bisa diganti di test.</param>
    public AppPaths(string exeDirectory)
    {
        ExeDirectory = Path.GetFullPath(exeDirectory);
        _data = new Lazy<(string, bool)>(ResolveDataDirectory);
    }

    public string ExeDirectory { get; }

    public string DataDirectory => _data.Value.Dir;
    public bool IsPortable => _data.Value.Portable;

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string ScrcpyBundleDirectory => Path.Combine(ExeDirectory, "scrcpy");
    public string ScrcpyManifestFile => Path.Combine(ExeDirectory, "config", "scrcpy-manifest.json");
    public string ScrcpyManifestOverrideFile => Path.Combine(DataDirectory, "scrcpy-manifest.json");

    public string WingetScrcpyLink => Path.Combine(LocalAppData, "Microsoft", "WinGet", "Links", "scrcpy.exe");

    public string AndroidSdkAdb => Path.Combine(LocalAppData, "Android", "Sdk", "platform-tools", "adb.exe");

    public string RecordingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), AppName);
    public string ScreenshotsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), AppName);
    public string DownloadsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", AppName);

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private (string Dir, bool Portable) ResolveDataDirectory()
    {
        var portable = Path.Combine(ExeDirectory, "data");
        if (IsWritableDirectory(portable))
        {
            return (portable, true);
        }

        var local = Path.Combine(LocalAppData, AppName);
        Directory.CreateDirectory(local);
        return (local, false);
    }

    /// <summary>Coba buat folder lalu tulis file sementara. Gagal = tidak bisa ditulisi (mis. Program Files).</summary>
    internal static bool IsWritableDirectory(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
