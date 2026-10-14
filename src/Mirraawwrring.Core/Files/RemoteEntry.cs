namespace Mirraawwrring.Core.Files;

/// <summary>Satu entri di penyimpanan HP (§5.9).</summary>
public sealed record RemoteEntry(string Path, bool IsDirectory, long Size, DateTimeOffset Modified)
{
    public string Name => RemotePath.NameOf(Path);
}

/// <summary>Operasi path POSIX untuk penyimpanan HP (selalu '/'; tidak memakai System.IO.Path yang bergaya Windows).</summary>
public static class RemotePath
{
    public const string Root = "/sdcard";

    /// <summary>Pintasan cepat di panel (nama tampil, path).</summary>
    public static IReadOnlyList<(string Name, string Path)> Shortcuts { get; } =
    [
        ("Download", Root + "/Download"),
        ("DCIM", Root + "/DCIM"),
        ("Pictures", Root + "/Pictures"),
        ("Documents", Root + "/Documents"),
        ("Movies", Root + "/Movies"),
    ];

    public static string Combine(string directory, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return directory.EndsWith('/') ? directory + name : directory + "/" + name;
    }

    public static string NameOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>Folder induk; null kalau sudah di root penyimpanan.</summary>
    public static string? ParentOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        if (trimmed.Length == 0 || string.Equals(trimmed, Root, StringComparison.Ordinal))
        {
            return null;
        }

        var slash = trimmed.LastIndexOf('/');
        return slash <= 0 ? "/" : trimmed[..slash];
    }

    /// <summary>Normalisasi: hilangkan '/' ganda dan di akhir, dan "." / ".." sederhana.</summary>
    public static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(part);
        }

        return "/" + string.Join('/', parts);
    }

    /// <summary>Segmen breadcrumb dari root penyimpanan: [("/sdcard"), ("/sdcard/DCIM"), …]. Path di luar root ditampilkan apa adanya.</summary>
    public static IReadOnlyList<(string Name, string Path)> Breadcrumb(string path)
    {
        var normalized = Normalize(path);
        var crumbs = new List<(string, string)>();
        if (normalized.StartsWith(Root + "/", StringComparison.Ordinal) || normalized == Root)
        {
            crumbs.Add((Root, Root));
            var rest = normalized.Length > Root.Length ? normalized[(Root.Length + 1)..] : string.Empty;
            var current = Root;
            foreach (var part in rest.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = Combine(current, part);
                crumbs.Add((part, current));
            }
        }
        else
        {
            crumbs.Add(("/", "/"));
            var current = string.Empty;
            foreach (var part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current += "/" + part;
                crumbs.Add((part, current));
            }
        }

        return crumbs;
    }

    /// <summary>Nama file valid di Android: tanpa '/', tanpa NUL, bukan "." / "..".</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name is not ("." or "..") && !name.Contains('/') && !name.Contains('\0');
}
