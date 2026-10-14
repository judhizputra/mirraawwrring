# Changelog

Semua perubahan penting dicatat di sini. Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/), versi mengikuti SemVer.

## [Unreleased]

### Fase 5 — Toolbar samping
- `DeviceActions` (satu-satunya tempat perintah tombol toolbar, §5.8): Power/Back/Home/Recent (`input keyevent`), volume dengan cadangan `cmd media_session volume --adj` saat injeksi diblokir, putar layar (`accelerometer_rotation 0` + `user_rotation`), notifikasi (`cmd statusbar expand-notifications`/`collapse`), tampilkan sentuhan, screenshot (`exec-out screencap -p` → `Pictures\Mirraawwrring\screenshot_<waktu>.png`, validasi tanda PNG, akhiran -2 bila nama bentrok), install APK (`install -r`, timeout 3 menit), teks info device; tabel aksi yang butuh injeksi; kombinasi tombol scrcpy (Ctrl+V, Alt+O, Alt+Shift+O).
- `Hotkey` (parser "Ctrl+Shift+S" → modifier + virtual key, murni) dan `DockPlacement` (posisi toolbar di kanan/kiri jendela scrcpy dalam piksel fisik, clamp ke area kerja monitor; konversi DIP ↔ piksel per DPI).
- `ToolbarWindow` (mockup Running-Toolbar): jendela transparan tanpa bingkai, owner = HWND scrcpy, `WS_EX_NOACTIVATE` (tidak merebut fokus keyboard), mengikuti gerak/resize/minimize/restore lewat `SetWinEventHook` yang difilter ke proses dan HWND scrcpy, menutup sendiri saat sesi berakhir, pindah ke kiri bila tidak muat di kanan. Tooltip dengan badge pintasan scrcpy (dari `reference/scrcpy-4.1-help.txt`), titik kuning + legenda saat `InjectionProbe` (diuji ulang tiap sesi) atau log scrcpy melaporkan INJECT_EVENTS; klik tombol yang diblokir membuka panduan Security settings.
- Hotkey screenshot `Ctrl+Shift+S` (`RegisterHotKey`, hanya terdaftar saat jendela scrcpy/toolbar jadi foreground; bisa diubah lewat `screenshotHotkey` di settings.json). Screenshot juga disalin ke clipboard.
- Rekam layar dari toolbar: proses scrcpy kedua `--no-window --no-playback --no-control --record=…mkv` (Matroska supaya file tetap bisa diputar saat proses dihentikan); tombol berubah merah; toast "Rekaman disimpan" dengan Buka/Folder.
- Kirim clipboard ke HP: fokuskan jendela scrcpy lalu `SendInput` Ctrl+V. Menu "Lainnya": tampilkan sentuhan (dicentang sesuai nilai HP), layar HP mati/nyala (Alt+O / Alt+Shift+O ke scrcpy), buka folder screenshot/rekaman, salin info device.
- Toast di samping toolbar (7 detik, bisa ditutup) untuk screenshot, rekaman, install APK, clipboard, dan kegagalan perintah.
- Tombol File browser sudah ada tetapi nonaktif sampai Fase 6.
- Diuji di PC ini dengan adb dan scrcpy palsu (scratchpad): docking kanan/kiri, mengikuti pindah jendela, minimize/restore, tutup otomatis, screenshot via tombol dan hotkey, Ctrl+V diterima jendela scrcpy, rekam mulai/berhenti, menu Lainnya, titik kuning + legenda + panduan. Belum diuji dengan HP sungguhan.
- 245 unit test.

### Fase 4 — Wizard setup
- `UsbProbe` (WMI `Win32_PnPEntity`): deteksi HP tercolok dari tabel VID (`UsbVendorTable`, mudah ditambah) / MTP, dan antarmuka ADB (`USB\Class_FF&SubClass_42&Prot_01`); `UsbWatcher` memakai `Win32_DeviceChangeEvent` + polling 2 detik; `PhoneStatusEvaluator` menggabungkan dengan adb (TidakTercolok / TercolokDebuggingMati / DebuggingAktifTapiAdbTidakMelihat / BelumDiizinkan / Siap).
- `InjectionProbe`: `adb shell input keyevent 0`, deteksi INJECT_EVENTS, cache per sesi per device, uji ulang.
- `SetupService`: unduh dari manifest dengan progress & bisa dibatalkan, verifikasi SHA-256 (file dihapus jika tidak cocok), ekstrak `folderInZip` ke `<exe>\scrcpy\` dengan perlindungan path traversal, hapus `Zone.Identifier`; winget (`Genymobile.scrcpy`) dengan output streaming; validasi folder yang sudah ada; cek internet (HEAD).
- `VerificationService`: file scrcpy lengkap, `scrcpy --version`, `adb version`, start-server (deteksi bentrok), adb lain di PATH (peringatan), HP diizinkan, uji injeksi; aksi perbaikan: unduh ulang, `"<adb lain>" kill-server`, panduan USB debugging, panduan Security settings.
- Jendela wizard (mockup Setup1–Setup4 termasuk 3b Gagal dan 3c HP): stepper, Deteksi (live saat USB berubah), Pasang (3 opsi, progress unduhan, output winget, dialog pilih folder), Verifikasi (live per baris, log, kartu error + tombol perbaikan, lewati cek HP), USB debugging belum aktif (langkah HyperOS + status HP tiap 2 detik), Selesai (versi, lokasi, kartu USB/Tanpa kabel, opsi cek pembaruan).
- Wizard muncul otomatis saat scrcpy/adb tidak ditemukan dan bisa dibuka dari tombol "Setup…" di bar status; setelah selesai, jendela utama inisialisasi ulang.
- 203 unit test.

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
