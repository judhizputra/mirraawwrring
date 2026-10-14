# Changelog

Semua perubahan penting dicatat di sini. Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/), versi mengikuti SemVer.

## [Unreleased]

### Fase 3 — Koneksi nirkabel
- `AddressUtil` (validasi IP:port, kode 6 digit, port default 5555) dan `WirelessService`: pair → tunggu koneksi otomatis via mDNS 5 detik → connect manual ke port koneksi; Sambung via IP dengan riwayat (maks 10, terbaru di depan); Aktifkan Wi-Fi dari USB (IP wlan0 → `adb tcpip 5555` → connect).
- `MdnsWatcher`: polling `adb mdns services` tiap 5 detik; HP yang Wireless debugging-nya aktif tetapi belum tersambung tampil di daftar perangkat sebagai kartu "HP di jaringan" dengan tombol Sambung.
- Dialog Pair wireless (mockup `Pair`): alamat penyambungan, 6 kotak digit (auto-maju, backspace mundur, paste 6 digit sekaligus), langkah 2 port koneksi aktif hanya jika koneksi otomatis gagal, status sukses/gagal dengan petunjuk.
- Dialog Sambung via IP: input + riwayat (klik untuk mengisi, bisa dihapus).
- Placeholder untuk TextBox lewat `Tag`.
- 155 unit test.

### Fase 2 — Jendela utama
- Layout sesuai mockup `Main`/`Empty`: panel perangkat (kartu dengan badge Terhubung / Belum diizinkan / Offline / Mirroring), state kosong dengan checklist, panel Mode (4 kartu), Kualitas (3 ComboBox), Opsi (6 checkbox), panel Log (bisa disalin, auto-scroll), bar status (adb siap / menunggu perangkat, versi scrcpy), tombol Buka folder rekaman dan Mulai/Hentikan mirroring.
- `DeviceWatcher` dihubungkan ke UI; info Android/pabrikan diambil lewat `getprop`; perangkat terakhir dipilih otomatis.
- `ScrcpySession` + `ScrcpySessionManager`: beberapa sesi sekaligus, scrcpy tanpa konsol, env `ADB`, log diklasifikasi, event INJECT_EVENTS → banner HyperOS + jendela panduan Security settings; HWND jendela scrcpy via `WindowFinder` (EnumWindows).
- `SettingsStore` (`settings.json`, atomic write, toleran file rusak): mode/kualitas/opsi dan perangkat terakhir diingat.
- Polling `adb devices` dicatat di level Debug (tidak membanjiri panel Log).
- Tombol Pair wireless / Sambung via IP / Aktifkan Wi-Fi dari USB sudah ada di UI, isinya menyusul di Fase 3.
- 116 unit test.

### Fase 1 — Core
- `IProcessRunner`/`ProcessRunner`: async, timeout, pembatalan, tangkap stdout/stderr/exit code, stdout biner, proses berjalan lama (`Start`), semua perintah dan output tercatat ke log.
- `ToolLocator` (§5.1): bundel → folder exe → path kustom → symlink winget (di-resolve) → PATH; adb diutamakan dari folder scrcpy; adb lain di PATH/Android SDK dilaporkan. `ToolRegistry` menyimpan lokasi aktif.
- `AdbClient`: devices, getprop, pair, connect, disconnect, tcpip, IP Wi-Fi, mdns services, start/kill server (deteksi bentrok versi), shell dengan escaping POSIX (`ShellQuoting`).
- Parser murni + test dari output nyata: `AdbDevicesParser`, `AdbPairResultParser`, `AdbConnectResultParser`, `AdbServerOutputParser`, `MdnsServicesParser`, `WifiIpParser`.
- `DeviceWatcher`: polling 2 detik, event hanya saat daftar berubah, kegagalan dilaporkan sekali per rentetan.
- `ScrcpyArgumentBuilder` (§5.5) dengan aturan konflik yang diuji; semua flag diverifikasi terhadap `scrcpy 4.1 --help` (`reference/scrcpy-4.1-help.txt`). Termasuk argumen rekam tanpa jendela.
- `ScrcpyLogClassifier` (§5.6): versi, info device, INJECT_EVENTS, tidak ada device, server gagal, device terputus, renderer.
- Aplikasi mencari tools saat startup dan mencatat hasilnya.
- 105 unit test.

### Fase 0 — Fondasi
- Solution .NET 10 / WPF dengan tiga proyek: `Mirraawwrring.Core`, `Mirraawwrring.App`, `Mirraawwrring.Core.Tests`.
- `Directory.Build.props` (versi aplikasi, LangVersion, Nullable, x64, TreatWarningsAsErrors untuk Core).
- App manifest Per-Monitor DPI Aware v2, `asInvoker`.
- DI dengan `Microsoft.Extensions.DependencyInjection`, MVVM dengan `CommunityToolkit.Mvvm`.
- Logging ke panel (`LogBuffer`) dan file harian di `data\logs` (simpan 7 hari) lewat `AppLoggerProvider`.
- `AppPaths`: mode portable (`data\` di samping exe) dengan fallback `%LOCALAPPDATA%\Mirraawwrring`.
- Tema gelap sendiri (`Themes/Colors.xaml`, `Themes/Controls.xaml`) sesuai token warna mockup; title bar native mode gelap lewat `DwmSetWindowAttribute`.
- `Strings.resx` + markup extension `{loc:Str Kunci}` untuk semua teks UI.
- `config/scrcpy-manifest.json` (scrcpy 4.1, SHA-256 terverifikasi terhadap unduhan resmi).
