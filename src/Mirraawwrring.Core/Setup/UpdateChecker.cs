using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Setup;

/// <summary>Rilis scrcpy terbaru di GitHub (dari API releases/latest).</summary>
public sealed record ScrcpyRelease(string TagName, string Version, string HtmlUrl, string? Win64ZipName, string? Win64ZipUrl);

/// <summary>
/// Hasil cek pembaruan. <see cref="VerifiedManifest"/> hanya terisi kalau SHA-256 ditemukan di <c>doc/windows.md</c>
/// pada tag rilis itu (PROMPT.md §5.7); tanpa itu aplikasi tidak boleh memasang otomatis, hanya membuka halaman rilis.
/// </summary>
public sealed record UpdateCheckResult(bool IsNewer, string InstalledVersion, ScrcpyRelease Latest, ScrcpyManifest? VerifiedManifest)
{
    public bool CanInstall => IsNewer && VerifiedManifest is not null;
}

/// <summary>Cek pembaruan scrcpy (opsional, §5.7). Fungsi parse dibuat statis murni agar bisa diuji dari respons nyata.</summary>
public sealed partial class UpdateChecker(HttpClient http, ILogger<UpdateChecker> logger)
{
    public const string LatestReleaseApi = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static string WindowsDocUrl(string tagName) => $"https://raw.githubusercontent.com/Genymobile/scrcpy/{tagName}/doc/windows.md";

    [GeneratedRegex(@"^scrcpy-win64-v[0-9][0-9A-Za-z.\-]*\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex Win64AssetRegex();

    [GeneratedRegex(@"\b([0-9a-fA-F]{64})\b")]
    private static partial Regex Sha256Regex();

    /// <summary>Null kalau gagal (offline, rate limit, format berubah); kegagalan hanya dicatat, tidak pernah mengganggu pengguna.</summary>
    public async Task<UpdateCheckResult?> CheckAsync(string installedVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Cek pembaruan scrcpy: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var latest = ParseLatestRelease(json);
            if (latest is null)
            {
                logger.LogDebug("Cek pembaruan scrcpy: respons tidak dikenali");
                return null;
            }

            var newer = CompareVersions(latest.Version, installedVersion) > 0;
            if (!newer)
            {
                logger.LogInformation("scrcpy {Installed} sudah yang terbaru (rilis: {Latest})", installedVersion, latest.Version);
                return new UpdateCheckResult(false, installedVersion, latest, null);
            }

            ScrcpyManifest? manifest = null;
            if (latest.Win64ZipName is not null && latest.Win64ZipUrl is not null)
            {
                var doc = await http.GetStringAsync(WindowsDocUrl(latest.TagName), cts.Token).ConfigureAwait(false);
                var sha = ParseWindowsSha256(doc, latest.Win64ZipName);
                if (sha is not null)
                {
                    manifest = new ScrcpyManifest
                    {
                        Version = latest.Version,
                        Url = latest.Win64ZipUrl,
                        Sha256 = sha,
                        FolderInZip = Path.GetFileNameWithoutExtension(latest.Win64ZipName),
                    };
                }
            }

            logger.LogInformation("scrcpy {Latest} tersedia (terpasang {Installed}); hash resmi {Hash}", latest.Version, installedVersion, manifest is null ? "tidak ditemukan" : "ditemukan");
            return new UpdateCheckResult(true, installedVersion, latest, manifest);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            logger.LogDebug(ex, "Cek pembaruan scrcpy gagal");
            return null;
        }
    }

    /// <summary>Parse JSON <c>releases/latest</c>: tag_name, html_url, dan aset <c>scrcpy-win64-v*.zip</c>.</summary>
    public static ScrcpyRelease? ParseLatestRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagElement) || tagElement.GetString() is not { Length: > 0 } tag)
        {
            return null;
        }

        var html = root.TryGetProperty("html_url", out var htmlElement) ? htmlElement.GetString() ?? string.Empty : string.Empty;
        string? zipName = null;
        string? zipUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is not null && Win64AssetRegex().IsMatch(name))
                {
                    zipName = name;
                    zipUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    break;
                }
            }
        }

        return new ScrcpyRelease(tag, NormalizeVersion(tag), html, zipName, zipUrl);
    }

    /// <summary>
    /// Cari SHA-256 untuk aset tertentu di <c>doc/windows.md</c>. Format (v2.x–v4.x):
    /// baris berisi nama zip, lalu baris berikutnya <c>&lt;sub&gt;SHA-256: `…`&lt;/sub&gt;</c>. Dicari di baris itu dan 3 baris setelahnya.
    /// </summary>
    public static string? ParseWindowsSha256(string markdown, string assetName)
    {
        var lines = markdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(assetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = i; j < Math.Min(lines.Length, i + 4); j++)
            {
                var match = Sha256Regex().Match(lines[j]);
                if (match.Success)
                {
                    return match.Groups[1].Value.ToLowerInvariant();
                }
            }
        }

        return null;
    }

    /// <summary>"v4.1" → "4.1"; "scrcpy 4.1 &lt;https…&gt;" → "4.1".</summary>
    public static string NormalizeVersion(string text)
    {
        var match = Regex.Match(text, @"\d+(?:\.\d+)*");
        return match.Success ? match.Value : text.Trim().TrimStart('v', 'V');
    }

    /// <summary>Bandingkan versi numerik ("4.1" &lt; "4.1.1" &lt; "5.0"); teks tak dikenal dianggap 0.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = Parts(a);
        var pb = Parts(b);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        return 0;

        static int[] Parts(string v) => NormalizeVersion(v).Split('.').Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).ToArray();
    }
}
