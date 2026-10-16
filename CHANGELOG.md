# Changelog

Semua perubahan penting dicatat di sini. Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/), versi mengikuti SemVer.

## [Unreleased]

### Lisensi (16 Okt 2026)
- File `LICENSE` (MIT, © 2026 Yudhistira Eka Putra) dan bagian Lisensi di README (scrcpy/ADB tetap Apache 2.0 dengan `LICENSE.txt` di folder `scrcpy\`).

### Minimize ke tray (16 Okt 2026)
- Minimize jendela utama menyembunyikannya ke ikon tray (area notifikasi), bukan ke taskbar; sesi scrcpy dan toolbar tetap berjalan. Klik kiri ikon = tampilkan lagi; klik kanan = menu Tampilkan/Keluar. Balon petunjuk muncul sekali per proses. Ikon tray hanya terlihat selama jendela disembunyikan (`Views/TrayIcon.cs`, WinForms `NotifyIcon`).
- Setting baru `minimizeToTray` (default true) di `settings.json`; belum ada UI.

### Perbaikan dari uji HP sungguhan (16 Okt 2026)
- **Ikon tombol Mulai/Hentikan mirroring**: saat HP yang dipilih sedang di-mirror, ikonnya menjadi kotak (stop), bukan segitiga (play).
- **HP terkunci di mode Layar HP mati** (laporan: tidak bisa membangunkan HP setelah terkunci otomatis):
  - `--stay-awake` hanya berlaku saat dicolok USB, jadi lewat Wi-Fi HP tetap tidur. Sekarang mode Layar HP mati dan opsi "Layar HP tetap menyala" juga mengirim `--screen-off-timeout=2147483` (nilai maksimum scrcpy; dikembalikan ke nilai awal saat scrcpy selesai), sehingga HP tidak terkunci otomatis selama mirroring lewat Wi-Fi sekalipun. Tooltip opsi menjelaskannya.
  - Tombol **Power** di toolbar membaca `dumpsys power` (`mWakefulness`): kalau HP tidur/dozing dikirim KEYCODE_WAKEUP (224, pasti membangunkan) + toast "HP dibangunkan — Masih gelap? Lainnya › Nyalakan layar HP"; kalau bangun, KEYCODE_POWER (26) seperti tombol fisik. Sebelumnya selalu POWER, yang justru menidurkan HP bila ternyata sudah bangun dengan layar gelap.
  - **Lainnya › Nyalakan layar HP** mengirim KEYCODE_WAKEUP dulu (bila injeksi tersedia) lalu Alt+Shift+O; Alt+Shift+O sendiri hanya menyalakan panel dan tidak membangunkan HP yang tidur.
- 323 unit test.

### Fase 8 — QR pairing & cek pembaruan scrcpy (opsional)
- **QR pairing** di dialog Pair wireless (tombol "Pakai QR code"): aplikasi membuat nama layanan `mirraawwrring-xxxxxxxx` + password acak, menampilkan QR berformat `WIFI:T:ADB;S:<nama>;P:<password>;;` (seperti Android Studio; pustaka QRCoder), lalu mem-poll `adb mdns services` tiap 1 detik sampai HP yang memindai QR mengumumkan layanan `_adb-tls-pairing._tcp` dengan nama itu, kemudian `adb pair host:port password` dan alur koneksi otomatis/port manual yang sama dengan mode kode. Tidak perlu server mDNS di PC. Timeout 5 menit; polling berhenti saat dialog ditutup.
- `WirelessService.PairWithPasswordAsync` (tanpa aturan 6 digit) dan `WaitForPairingServiceAsync`; `QrPairing` (sesi QR, pencarian layanan) murni dan diuji.
- **Cek pembaruan scrcpy** (`UpdateChecker`, hanya jika "Periksa pembaruan" aktif di settings): saat mulai, membandingkan tag `releases/latest` GitHub dengan versi `scrcpy --version`. Jika lebih baru, tautan "scrcpy X tersedia" muncul di bar status. Pasang otomatis hanya bila SHA-256 untuk `scrcpy-win64-vX.zip` ditemukan di `doc/windows.md` pada tag itu DAN scrcpy yang dipakai berasal dari folder `scrcpy\` aplikasi; prosesnya: hentikan polling + `adb kill-server`, unduh, verifikasi hash, ekstrak menimpa, simpan manifest baru ke `data\scrcpy-manifest.json` (dipakai wizard untuk unduh ulang), cari ulang tools, inisialisasi ulang. Kalau tidak memenuhi syarat, hanya menawarkan membuka halaman rilis.
- Diuji di PC ini: QR pairing end-to-end dengan adb palsu (layanan mDNS bernama sesuai QR → pair → tersambung otomatis → dialog tertutup, kartu Wi-Fi muncul); cek pembaruan nyata menemukan scrcpy 5.0.1 dengan hash resmi dan pemasangannya ke folder `scrcpy\` berjalan (lihat catatan keterbatasan: flag diverifikasi untuk 4.1).
- 310 unit test.

### Fase 7 — Distribusi & dokumentasi
- `scripts/publish.ps1`: versi dari `Directory.Build.props`, unit test (bisa dilewati `-SkipTests`), `dotnet publish` win-x64 self-contained single-file terkompresi ke `dist\Mirraawwrring-<versi>\` (exe ±60 MB, plus `config\scrcpy-manifest.json`, README, CHANGELOG), `-BundleScrcpy` mengunduh scrcpy sesuai manifest, memverifikasi SHA-256 (tidak cocok = berhenti dan hapus), mengekstrak ke `scrcpy\` dan Unblock-File; zip `dist\Mirraawwrring-<versi>-win-x64.zip` (`-NoZip` untuk melewati).
- Diuji: publish dengan `-BundleScrcpy` di PC ini (hash cocok), exe hasil publish berjalan portable (`data\` di samping exe) dan menemukan scrcpy bundel ("menunggu perangkat · scrcpy 4.1").
- README final (paket rilis, Unblock/SmartScreen, publish, toolbar, file browser, masalah umum), CLAUDE.md final, checklist Fase 7 di `docs/manual-test.md`.

### Fase 6 — File browser
- `RemoteEntry`/`RemotePath` (operasi path POSIX, breadcrumb, pintasan Download/DCIM/Pictures/Documents/Movies, validasi nama) dan `RemoteListingParser` (murni): output `find DIR -maxdepth 1 -mindepth 1 -exec stat -c '%F|%s|%Y|%n' {} +`, nama dengan `|` aman, baris error (Permission denied) dikumpulkan, folder dulu lalu nama.
- `RemoteFileService`: listing, pull/push satu per satu lewat `adb pull/push` dengan progres per file + total byte (bisa dibatalkan), `mkdir -p`, `mv`, `rm -r` (konfirmasi di UI), folder tujuan default `Downloads\Mirraawwrring` (bisa diubah, disimpan di `pullDirectory`).
- Panel file browser (mockup Running-Files) di dalam jendela toolbar, menempel di kanan toolbar dan bisa di-resize lewat pegangan di tepi luar (lebar disimpan di `filesPanelWidth`): header + nama HP, chip pintasan, breadcrumb, Simpan ke PC (n), Kirim ke HP (dialog pilih banyak file), folder baru / ganti nama (editor inline, Enter/Esc), Hapus dengan dialog konfirmasi berisi daftar item, pencarian lokal, daftar dengan checkbox + ikon jenis (folder/gambar/video), baris ".. (induk)", klik ganda membuka folder, area drop (drag & drop dari Explorer; .apk ditawarkan untuk di-install, sisanya dikirim), bar progres transfer dengan Batalkan, toast hasil (Folder/Buka).
- Tombol File browser di toolbar aktif (hijau saat panel terbuka).
- Diuji di PC ini dengan adb palsu berbasis folder lokal: navigasi chip/klik ganda/induk, pilih 2 file → pull dengan progres → toast → file ada di Downloads, folder baru, ganti nama, filter, hapus (dialog konfirmasi, Tidak/Ya), resize panel, tutup panel. Push lewat dialog file dan drag & drop belum diuji otomatis (logikanya sama dengan pull dan tercakup unit test).
- 291 unit test.

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
