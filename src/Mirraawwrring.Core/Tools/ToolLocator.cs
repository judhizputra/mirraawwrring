using Mirraawwrring.Core.IO;

namespace Mirraawwrring.Core.Tools;

public interface IToolLocator
{
    /// <param name="customScrcpyPath">Path kustom dari settings: boleh folder atau path lengkap scrcpy.exe.</param>
    ToolLocation Locate(string? customScrcpyPath = null);
}

/// <summary>
/// Satu-satunya tempat yang tahu cara mencari scrcpy.exe dan adb.exe (§5.1).
/// Urutan scrcpy: folder bundel, folder exe, path kustom, symlink winget, PATH.
/// adb dicari dulu di folder scrcpy yang ketemu supaya versinya cocok.
/// </summary>
public sealed class ToolLocator(IAppPaths paths, IFileSystem fs) : IToolLocator
{
    public const string ScrcpyExe = "scrcpy.exe";
    public const string AdbExe = "adb.exe";

    public ToolLocation Locate(string? customScrcpyPath = null)
    {
        var customDir = NormalizeCustomDirectory(customScrcpyPath);
        var pathDirs = PathDirectories();

        var (scrcpyPath, scrcpySource) = LocateScrcpy(customDir, pathDirs);

        // Kandidat adb, berurutan. Folder scrcpy (hasil resolve symlink) paling dulu.
        var candidates = new List<(string Path, ToolSource Source)>();
        if (scrcpyPath is not null)
        {
            candidates.Add((Path.Combine(Path.GetDirectoryName(scrcpyPath)!, AdbExe), ToolSource.ScrcpyFolder));
        }

        candidates.Add((Path.Combine(paths.ScrcpyBundleDirectory, AdbExe), ToolSource.BundledFolder));
        candidates.Add((Path.Combine(paths.ExeDirectory, AdbExe), ToolSource.ExeFolder));
        if (customDir is not null)
        {
            candidates.Add((Path.Combine(customDir, AdbExe), ToolSource.CustomPath));
        }

        candidates.Add((Path.Combine(Path.GetDirectoryName(paths.WingetScrcpyLink)!, AdbExe), ToolSource.Winget));
        foreach (var dir in pathDirs)
        {
            candidates.Add((Path.Combine(dir, AdbExe), ToolSource.Path));
        }

        candidates.Add((paths.AndroidSdkAdb, ToolSource.AndroidSdk));

        string? adbPath = null;
        ToolSource? adbSource = null;
        var others = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (candidate, source) in candidates)
        {
            var resolved = ResolveExisting(candidate);
            if (resolved is null || !seen.Add(resolved))
            {
                continue;
            }

            if (adbPath is null)
            {
                adbPath = resolved;
                adbSource = source;
            }
            else if (source is ToolSource.Path or ToolSource.AndroidSdk or ToolSource.Winget)
            {
                others.Add(resolved);
            }
        }

        return new ToolLocation
        {
            ScrcpyPath = scrcpyPath,
            ScrcpySource = scrcpySource,
            AdbPath = adbPath,
            AdbSource = adbSource,
            OtherAdbPaths = others,
        };
    }

    private (string? Path, ToolSource? Source) LocateScrcpy(string? customDir, IReadOnlyList<string> pathDirs)
    {
        var candidates = new List<(string Path, ToolSource Source)>
        {
            (Path.Combine(paths.ScrcpyBundleDirectory, ScrcpyExe), ToolSource.BundledFolder),
            (Path.Combine(paths.ExeDirectory, ScrcpyExe), ToolSource.ExeFolder),
        };
        if (customDir is not null)
        {
            candidates.Add((Path.Combine(customDir, ScrcpyExe), ToolSource.CustomPath));
        }

        candidates.Add((paths.WingetScrcpyLink, ToolSource.Winget));
        foreach (var dir in pathDirs)
        {
            candidates.Add((Path.Combine(dir, ScrcpyExe), ToolSource.Path));
        }

        foreach (var (candidate, source) in candidates)
        {
            var resolved = ResolveExisting(candidate);
            if (resolved is not null)
            {
                return (resolved, source);
            }
        }

        return (null, null);
    }

    /// <summary>Path file jika ada, mengikuti symlink ke target aslinya (winget memasang scrcpy sebagai link).</summary>
    private string? ResolveExisting(string candidate)
    {
        if (!fs.FileExists(candidate))
        {
            return null;
        }

        var target = fs.ResolveLinkTarget(candidate);
        if (target is not null && fs.FileExists(target))
        {
            return Path.GetFullPath(target);
        }

        return Path.GetFullPath(candidate);
    }

    private string? NormalizeCustomDirectory(string? custom)
    {
        if (string.IsNullOrWhiteSpace(custom))
        {
            return null;
        }

        custom = custom.Trim().Trim('"');
        if (custom.EndsWith(ScrcpyExe, StringComparison.OrdinalIgnoreCase) || fs.FileExists(custom))
        {
            return Path.GetDirectoryName(Path.GetFullPath(custom));
        }

        return Path.GetFullPath(custom);
    }

    private List<string> PathDirectories()
    {
        var raw = fs.GetEnvironmentVariable("PATH") ?? string.Empty;
        var result = new List<string>();
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dir = part.Trim('"');
            if (dir.Length > 0 && !result.Contains(dir, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(dir);
            }
        }

        return result;
    }
}
