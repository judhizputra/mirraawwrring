using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mirraawwrring.Core.Setup;

/// <summary>Isi <c>config/scrcpy-manifest.json</c>: versi scrcpy yang di-pin, URL unduhan, SHA-256, dan nama folder di dalam zip.</summary>
public sealed record ScrcpyManifest
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }

    [JsonPropertyName("folderInZip")]
    public required string FolderInZip { get; init; }

    public string FileName => Path.GetFileName(new Uri(Url).LocalPath);

    private static readonly JsonSerializerOptions Options = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static ScrcpyManifest Parse(string json) =>
        JsonSerializer.Deserialize<ScrcpyManifest>(json, Options) ?? throw new InvalidDataException("Manifest kosong.");

    public static ScrcpyManifest Load(string path) => Parse(File.ReadAllText(path));
}
