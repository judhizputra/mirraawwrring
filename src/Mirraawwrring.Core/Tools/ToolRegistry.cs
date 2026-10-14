namespace Mirraawwrring.Core.Tools;

/// <summary>
/// Menyimpan lokasi tools yang sedang dipakai aplikasi. Diisi saat startup (dan ulang oleh wizard setup),
/// dibaca oleh AdbClient dan ScrcpySession. Singleton di DI.
/// </summary>
public interface IToolRegistry
{
    ToolLocation Current { get; }

    event Action<ToolLocation>? Changed;

    void Update(ToolLocation location);

    /// <summary>Path adb aktif, atau lempar <see cref="ToolNotFoundException"/>.</summary>
    string RequireAdb();

    string RequireScrcpy();
}

public sealed class ToolRegistry : IToolRegistry
{
    private ToolLocation _current = ToolLocation.NotFound;

    public ToolLocation Current => Volatile.Read(ref _current);

    public event Action<ToolLocation>? Changed;

    public void Update(ToolLocation location)
    {
        Volatile.Write(ref _current, location);
        Changed?.Invoke(location);
    }

    public string RequireAdb() => Current.AdbPath ?? throw new ToolNotFoundException("adb.exe");

    public string RequireScrcpy() => Current.ScrcpyPath ?? throw new ToolNotFoundException("scrcpy.exe");
}

public sealed class ToolNotFoundException(string toolName) : Exception($"{toolName} tidak ditemukan. Jalankan wizard setup.")
{
    public string ToolName { get; } = toolName;
}
