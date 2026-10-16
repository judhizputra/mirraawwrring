using Mirraawwrring.Core.Scrcpy;

namespace Mirraawwrring.Core.Settings;

/// <summary>Pengaturan yang disimpan di <c>settings.json</c>. Tambahkan properti baru dengan nilai default agar file lama tetap terbaca.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    /// <summary>Pilihan mode/kualitas/opsi terakhir di jendela utama.</summary>
    public ScrcpyOptions Options { get; set; } = new();

    /// <summary>Folder atau path scrcpy.exe pilihan pengguna (wizard "folder yang sudah ada").</summary>
    public string? CustomScrcpyPath { get; set; }

    /// <summary>Serial perangkat yang terakhir dipilih, untuk dipilih otomatis saat muncul lagi.</summary>
    public string? LastDeviceSerial { get; set; }

    /// <summary>Riwayat alamat "Sambung via IP" (terbaru di depan).</summary>
    public List<string> ConnectionHistory { get; set; } = [];

    public bool CheckScrcpyUpdates { get; set; } = true;

    /// <summary>Hotkey screenshot milik aplikasi, format "Ctrl+Shift+S".</summary>
    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+S";

    /// <summary>Folder tujuan "Simpan ke PC" di file browser; null = Downloads\Mirraawwrring.</summary>
    public string? PullDirectory { get; set; }

    /// <summary>Lebar panel file browser di samping toolbar (DIP), bisa di-resize.</summary>
    public double FilesPanelWidth { get; set; } = 496;

    /// <summary>Minimize jendela utama = sembunyikan ke ikon tray (bukan ke taskbar). Belum ada UI; ubah di settings.json.</summary>
    public bool MinimizeToTray { get; set; } = true;
}
