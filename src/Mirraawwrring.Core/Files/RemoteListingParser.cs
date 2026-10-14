using System.Globalization;

namespace Mirraawwrring.Core.Files;

/// <summary>
/// Parser murni untuk output <c>find DIR -maxdepth 1 -mindepth 1 -exec stat -c '%F|%s|%Y|%n' {} +</c> (toybox Android):
/// satu baris per entri, <c>tipe|ukuran|mtime-epoch|path</c>. Baris yang tidak cocok (mis. "Permission denied") dilewati
/// dan dikumpulkan di <see cref="ListingResult.Errors"/>.
/// </summary>
public static class RemoteListingParser
{
    public const string StatFormat = "%F|%s|%Y|%n";

    public static ListingResult Parse(string output)
    {
        var entries = new List<RemoteEntry>();
        var errors = new List<string>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            if (TryParseLine(line, out var entry))
            {
                entries.Add(entry);
            }
            else
            {
                errors.Add(line);
            }
        }

        entries.Sort(Compare);
        return new ListingResult(entries, errors);
    }

    /// <summary>Folder dulu, lalu nama (tanpa memperhatikan huruf besar/kecil).</summary>
    public static int Compare(RemoteEntry a, RemoteEntry b)
    {
        if (a.IsDirectory != b.IsDirectory)
        {
            return a.IsDirectory ? -1 : 1;
        }

        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseLine(string line, out RemoteEntry entry)
    {
        entry = null!;
        // Nama file boleh mengandung '|', jadi hanya 3 pemisah pertama yang dipakai.
        var parts = line.Split('|', 4);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            || !long.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var mtime)
            || parts[3].Length == 0)
        {
            return false;
        }

        var isDir = parts[0].Trim().Equals("directory", StringComparison.OrdinalIgnoreCase);
        entry = new RemoteEntry(parts[3], isDir, isDir ? 0 : size, DateTimeOffset.FromUnixTimeSeconds(mtime).ToLocalTime());
        return true;
    }
}

public sealed record ListingResult(IReadOnlyList<RemoteEntry> Entries, IReadOnlyList<string> Errors);
