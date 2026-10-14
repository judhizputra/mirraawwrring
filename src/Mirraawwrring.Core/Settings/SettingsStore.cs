using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Settings;

public interface ISettingsStore
{
    /// <summary>Pengaturan yang sedang dipakai (dimuat sekali saat pertama diakses).</summary>
    AppSettings Current { get; }

    /// <summary>Ubah lalu simpan. Aman dipanggil dari thread mana pun.</summary>
    void Update(Action<AppSettings> change);

    void Save();
}

/// <summary>Baca/tulis <c>settings.json</c> di <see cref="IAppPaths.SettingsFile"/>. File rusak → default + peringatan di log.</summary>
public sealed class SettingsStore(IAppPaths paths, ILogger<SettingsStore> logger) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // null harus ikut ditulis: MaxSize/MaxFps null berarti "Asli"/"Tanpa batas", bukan default.
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly object _gate = new();
    private AppSettings? _current;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current ??= Load();
            }
        }
    }

    public void Update(Action<AppSettings> change)
    {
        lock (_gate)
        {
            change(Current);
            SaveLocked();
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            SaveLocked();
        }
    }

    private AppSettings Load()
    {
        var file = paths.SettingsFile;
        if (!File.Exists(file))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "settings.json tidak bisa dibaca, memakai pengaturan default");
            return new AppSettings();
        }
    }

    private void SaveLocked()
    {
        var file = paths.SettingsFile;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(tmp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Gagal menyimpan settings.json");
        }
    }
}
