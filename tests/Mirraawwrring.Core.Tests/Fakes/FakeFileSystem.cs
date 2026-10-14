using Mirraawwrring.Core;
using Mirraawwrring.Core.IO;

namespace Mirraawwrring.Core.Tests;

/// <summary>File system palsu: daftar file, symlink, dan variabel environment.</summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _env = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem AddFile(params string[] paths)
    {
        foreach (var p in paths)
        {
            _files.Add(Path.GetFullPath(p));
        }

        return this;
    }

    public FakeFileSystem AddLink(string link, string target)
    {
        _files.Add(Path.GetFullPath(link));
        _links[Path.GetFullPath(link)] = Path.GetFullPath(target);
        return this;
    }

    public FakeFileSystem SetEnv(string name, string? value)
    {
        if (value is null)
        {
            _env.Remove(name);
        }
        else
        {
            _env[name] = value;
        }

        return this;
    }

    public bool FileExists(string path) => _files.Contains(Path.GetFullPath(path));

    public bool DirectoryExists(string path) =>
        _files.Any(f => f.StartsWith(Path.GetFullPath(path).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

    public string? ResolveLinkTarget(string path) => _links.GetValueOrDefault(Path.GetFullPath(path));

    public string? GetEnvironmentVariable(string name) => _env.GetValueOrDefault(name);
}

/// <summary>IAppPaths palsu dengan semua lokasi di bawah satu root buatan.</summary>
public sealed class FakeAppPaths : IAppPaths
{
    public FakeAppPaths(string exeDirectory = @"C:\apps\Mirraawwrring")
    {
        ExeDirectory = exeDirectory;
    }

    public string ExeDirectory { get; }
    public string DataDirectory => Path.Combine(ExeDirectory, "data");
    public bool IsPortable => true;
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string ScrcpyBundleDirectory => Path.Combine(ExeDirectory, "scrcpy");
    public string ScrcpyManifestFile => Path.Combine(ExeDirectory, "config", "scrcpy-manifest.json");
    public string WingetScrcpyLink { get; init; } = @"C:\Users\u\AppData\Local\Microsoft\WinGet\Links\scrcpy.exe";
    public string AndroidSdkAdb { get; init; } = @"C:\Users\u\AppData\Local\Android\Sdk\platform-tools\adb.exe";
    public string RecordingsDirectory { get; init; } = @"C:\Users\u\Videos\Mirraawwrring";
    public string ScreenshotsDirectory { get; init; } = @"C:\Users\u\Pictures\Mirraawwrring";
    public string DownloadsDirectory { get; init; } = @"C:\Users\u\Downloads\Mirraawwrring";
}
