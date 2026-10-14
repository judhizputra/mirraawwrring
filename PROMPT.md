# Mirraawwrring — Spesifikasi Proyek untuk Claude Code

Kamu akan membangun **Mirraawwrring**, aplikasi desktop Windows (GUI) untuk mengontrol dan me-mirror HP Android dari PC, dengan **scrcpy** sebagai mesin utamanya. Pemilik proyek ingin source code yang rapi, teruji, dan mudah dikembangkan kapan saja, baik oleh dirinya maupun oleh sesi Claude Code berikutnya.

Baca seluruh dokumen ini sebelum mulai. Kerjakan per fase (lihat bagian **Urutan kerja**). Kalau ada keputusan yang menyimpang dari spesifikasi ini, tanyakan dulu.

---

## 1. Materi referensi

**Desain (sumber utama):** kanvas mockup di **https://claude.ai/artifact/CgXACbkFrCm9jG2R11xtyK**. Buka dan pelajari semua artboard-nya sebelum membuat UI. Ini artifact claude.ai: baca dengan Artifact tool (action `read`, juga untuk file `project/*.dc.html` di dalamnya), bukan dengan WebFetch atau curl. Kanvas ini bisa saja diperbarui pemilik setelah dokumen ini ditulis. Kalau isinya berbeda dengan salinan di `reference/mockups/`, **yang di link adalah versi terbaru**. Link ini publik. Kalau tetap tidak bisa dibaca dari sesimu (misalnya Artifact tool tidak tersedia), katakan kepada pemilik lalu pakai salinan di `reference/mockups/`.

File di folder `reference/`:

- `reference/scrcpy-launcher.ps1` — **prototipe yang sudah dipakai dan diuji**. Logika di dalamnya (mencari scrcpy/adb, mengikuti symlink winget, memakai adb bawaan scrcpy lewat env `ADB`, parsing `adb devices`, alur `adb pair` → tunggu koneksi mDNS → `adb connect`, menyimpan IP terakhir) sudah terbukti jalan. Pakai sebagai acuan perilaku, lalu tulis ulang dengan arsitektur yang benar dalam C#.
- `reference/mockups/*.dc.html` — salinan mockup desain semua layar dari kanvas di atas (per 14 Oktober 2026). Formatnya format kanvas desain khusus: abaikan `<x-dc>`, `<helmet>`, `<sc-*>`, `{{...}}`, dan blok `<script type="text/x-dc">`. Ambil **tata letak, warna, ukuran, teks UI, dan state** dari inline style dan markup-nya. Daftar layar ada di `reference/mockups/README.md`. Mockup masih memakai nama kerja lama "scrcpy Launcher" dan folder `Videos\scrcpy`; di aplikasi, ganti semuanya menjadi **Mirraawwrring**.
- `reference/icon.ico` — ikon sementara (boleh diperbaiki).

Perangkat uji utama pemilik: **Xiaomi 2407FPN8EG, HyperOS, Android 16**. HP ini memblokir injeksi input sampai opsi *USB debugging (Security settings)* aktif. Log nyata saat diblokir:

```
[server] ERROR: Injecting input events requires the caller (or the source of the instrumentation, if any) to have the INJECT_EVENTS permission.
[server] ERROR: Make sure you have enabled "USB debugging (Security Settings)" and then rebooted your device.
```

---

## 2. Tech stack (wajib)

- **.NET 10 (LTS), WPF, C#** (bahasa versi terbaru), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, target `net10.0-windows`, x64.
- **CommunityToolkit.Mvvm** untuk MVVM (ObservableObject, RelayCommand, source generators).
- **Microsoft.Extensions.DependencyInjection** untuk DI. Jangan pakai framework UI pihak ketiga yang berat. Buat tema sendiri lewat ResourceDictionary.
- **xUnit** untuk unit test proyek Core.
- Target OS: Windows 10 1809+ dan Windows 11.
- App manifest: **Per-Monitor DPI Aware v2**, `asInvoker`.

Alasan: aplikasi ini banyak memakai Win32 interop (pelacakan posisi jendela, WMI untuk USB, manajemen proses). C# + WPF paling stabil untuk itu, dan hasilnya bisa dipublikasikan sebagai satu exe portable.

---

## 3. Struktur repo

```
Mirraawwrring/
├─ Mirraawwrring.sln
├─ Directory.Build.props          # versi aplikasi, LangVersion, Nullable, TreatWarningsAsErrors untuk Core
├─ CLAUDE.md                      # peta arsitektur + konvensi + cara menambah fitur (WAJIB, lihat §12)
├─ README.md                      # bahasa Indonesia
├─ CHANGELOG.md
├─ config/
│  └─ scrcpy-manifest.json        # versi scrcpy yang di-pin + URL + SHA-256
├─ docs/
│  └─ manual-test.md              # checklist uji manual dengan HP sungguhan
├─ scripts/
│  └─ publish.ps1                 # build portable + zip
├─ src/
│  ├─ Mirraawwrring.Core/         # TANPA referensi WPF. Logika, adb, scrcpy, parser, layanan.
│  └─ Mirraawwrring.App/          # WPF: Views, ViewModels, tema, interop jendela
└─ tests/
   └─ Mirraawwrring.Core.Tests/
```

Isi awal `config/scrcpy-manifest.json` (hash sudah diverifikasi dengan dokumentasi resmi scrcpy):

```json
{
  "version": "4.1",
  "url": "https://github.com/Genymobile/scrcpy/releases/download/v4.1/scrcpy-win64-v4.1.zip",
  "sha256": "5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db",
  "folderInZip": "scrcpy-win64-v4.1"
}
```

---

## 4. Prinsip arsitektur

1. **Core tidak tahu UI.** Semua pemanggilan adb/scrcpy lewat interface `IProcessRunner` (async, bisa dibatalkan, ada timeout, menangkap stdout/stderr/exit code). Di test, `IProcessRunner` diganti fake.
2. **Parser adalah fungsi murni** dengan unit test dari contoh output nyata (lihat §11). Contoh: `AdbDevicesParser`, `AdbPairResultParser`, `ScrcpyLogClassifier`, `RemoteFileListParser`.
3. **UI thread tidak boleh terblokir.** Semua perintah eksternal `async`, dengan timeout yang masuk akal (mis. 10 detik untuk perintah adb biasa).
4. **Satu tempat untuk setiap pengetahuan.** Pemetaan opsi → argumen scrcpy hanya di `ScrcpyArgumentBuilder`. Perintah tombol toolbar hanya di `DeviceActions`. Lokasi tools hanya di `ToolLocator`.
5. **Semua perintah tercatat.** Perintah yang dijalankan dan outputnya masuk ke panel Log dan ke file log harian (simpan 7 hari terakhir).
6. **Teks UI bahasa Indonesia**, dikumpulkan di satu file resource (`Strings.resx`) supaya kelak bisa diterjemahkan.
7. **Jangan menulis path secara hardcode** selain lewat konstanta di satu tempat.
8. **Mode portable**: data (settings.json, logs) disimpan di folder `data\` di samping exe kalau folder itu bisa ditulisi; kalau tidak, di `%LOCALAPPDATA%\Mirraawwrring\`.
9. Jika ragu soal nama flag atau shortcut scrcpy, **cek `scrcpy --help` dari versi yang dibundel** dan dokumentasi resmi scrcpy versi itu. Jangan menebak.

---

## 5. Layanan inti (Core)

### 5.1 ToolLocator
Urutan pencarian `scrcpy.exe`:
1. `<folder exe>\scrcpy\`
2. `<folder exe>\`
3. Path kustom dari settings
4. Hasil winget: `%LOCALAPPDATA%\Microsoft\WinGet\Links\scrcpy.exe`. Ini symlink; resolve ke target aslinya.
5. `PATH`

`adb.exe` dicari **pertama di folder yang sama dengan scrcpy.exe yang ditemukan** (agar versinya cocok), baru kemudian lokasi lain. Saat menjalankan scrcpy, set environment variable `ADB` ke path adb tersebut. Deteksi juga adb lain di `PATH` (mis. Android SDK) untuk peringatan bentrok versi.

### 5.2 AdbClient
- `GetDevicesAsync()` → `adb devices -l`, diparse menjadi `Device { Serial, State, Model, Transport (Usb/WifiIp/WifiMdns), TransportId }`. State yang dikenali: `device`, `unauthorized`, `offline`, `no permissions`, `recovery`, `sideload`. Serial seperti `adb-XXXX._adb-tls-connect._tcp` atau `IP:port` berarti Wi-Fi.
- Info tambahan via `getprop`: `ro.product.manufacturer`, `ro.product.model`, `ro.build.version.release`.
- `PairAsync(hostPort, code)`, `ConnectAsync(hostPort)`, `DisconnectAsync`, `TcpIpAsync(serial, 5555)`, `GetWifiIpAsync(serial)`.
- `MdnsServicesAsync()` → `adb mdns services` untuk HP yang sudah di-pair dan Wireless debugging-nya aktif di jaringan.
- `StartServerAsync` dan `KillServerAsync`, dengan deteksi pesan bentrok versi.
- **DeviceWatcher**: polling `adb devices -l` tiap 2 detik (atau `adb track-devices` jika stabil), mengeluarkan event saat daftar berubah.

### 5.3 UsbProbe — deteksi USB debugging TANPA adb
Gunakan WMI (`System.Management`, kelas `Win32_PnPEntity`):
- **HP tercolok**: ada perangkat USB Android yang dikenali dari Vendor ID (`VID_xxxx`) atau antarmuka MTP/PTP. Buat tabel VID → merek yang mudah ditambah, misalnya Xiaomi `2717`, Samsung `04E8`, Google `18D1`, OPPO/realme/OnePlus `22D9`, vivo `2D95`, Huawei `12D1`, Motorola `22B8`.
- **USB debugging aktif**: ada antarmuka dengan CompatibleID yang mengandung `USB\Class_FF&SubClass_42&Prot_01` (antarmuka ADB).
- Hasil gabungan dengan data adb: `TidakTercolok`, `TercolokDebuggingMati`, `DebuggingAktifTapiAdbTidakMelihat` (sarankan driver USB), `BelumDiizinkan` (adb `unauthorized`), `Siap`.
- Pantau perubahan lewat `ManagementEventWatcher` (`Win32_DeviceChangeEvent`), dengan polling 2 detik sebagai cadangan.

### 5.4 InjectionProbe (khusus Xiaomi/HyperOS dan sejenisnya)
Uji sekali per sesi per device: `adb -s <serial> shell input keyevent 0`. Jika stderr/stdout mengandung `INJECT_EVENTS`, tandai device sebagai **input diblokir**. Uji ulang saat pengguna menekan "Periksa ulang".

### 5.5 ScrcpyArgumentBuilder
| Pilihan UI | Argumen |
|---|---|
| Mode: Mirror + kontrol | (default) |
| Mode: Lihat saja | `--no-control` |
| Mode: Layar HP mati | `--turn-screen-off --stay-awake` |
| Mode: Hemat data | `--max-size=1024 --video-bit-rate=4M --max-fps=30` |
| Resolusi maks | `--max-size=<n>` (Asli = tidak dikirim) |
| Bitrate video | `--video-bit-rate=<n>M` |
| FPS maks | `--max-fps=<n>` |
| Teruskan audio = mati | `--no-audio` |
| Input UHID | `--keyboard=uhid --mouse=uhid` |
| Jendela selalu di atas | `--always-on-top` |
| Mulai layar penuh | `--fullscreen` |
| Layar HP tetap menyala | `--stay-awake` |
| Rekam ke file | `--record=<Videos\Mirraawwrring\scrcpy_yyyyMMdd_HHmmss.mp4>` |
| Selalu | `--serial=<serial>` dan `--window-title=Mirraawwrring - <model>` |

Pilihan yang bentrok (mis. Hemat data + resolusi manual) diselesaikan dengan aturan yang jelas dan diuji. Buat unit test untuk builder ini.

### 5.6 ScrcpySession
- Menjalankan scrcpy tanpa jendela konsol (`CreateNoWindow`, stdout/stderr dialihkan ke log).
- `ScrcpyLogClassifier` mengenali pesan penting: INJECT_EVENTS (tampilkan banner HyperOS + sarankan UHID), `Could not find any ADB device`, device terputus, dan server gagal.
- Mendukung **beberapa sesi sekaligus** (beberapa HP), masing-masing dengan toolbarnya sendiri.
- Menyediakan HWND jendela scrcpy setelah muncul: `EnumWindows` + `GetWindowThreadProcessId` cocok dengan PID, tunggu maks 10 detik.

### 5.7 SetupService (unduh & pasang scrcpy)
- Unduh dari `scrcpy-manifest.json` dengan progress dan bisa dibatalkan. **Verifikasi SHA-256**; jika tidak cocok, hapus file dan tampilkan error.
- Ekstrak ke `<folder exe>\scrcpy\` (isi `folderInZip`), lalu hapus alternate data stream `Zone.Identifier` dari semua file hasil ekstrak (unblock).
- Opsi winget: jalankan `winget install --id Genymobile.scrcpy -e --accept-source-agreements --accept-package-agreements`, tampilkan outputnya di wizard.
- Opsi folder yang sudah ada: validasi ada `scrcpy.exe`, `adb.exe`, dan `scrcpy-server`.
- Cek pembaruan (opsional, bisa dimatikan): bandingkan tag rilis terbaru scrcpy di GitHub dengan versi terpasang. Untuk memasang versi baru, ambil SHA-256 dari `doc/windows.md` pada tag tersebut. Kalau hash tidak bisa ditemukan, **jangan pasang otomatis**; arahkan pengguna ke halaman rilis.

### 5.8 DeviceActions (perintah tombol toolbar)
| Tombol | Perintah utama | Butuh injeksi | Cadangan jika diblokir |
|---|---|---|---|
| Power | `input keyevent 26` | ya | — |
| Volume naik/turun | `input keyevent 24` / `25` | ya | `cmd media_session volume --adj raise` / `lower` (verifikasi di device) |
| Putar layar | `settings put system accelerometer_rotation 0` + `settings put system user_rotation <0..3>` | tidak | — |
| Back / Home / Recent | `input keyevent 4` / `3` / `187` | ya | — |
| Notifikasi | `cmd statusbar expand-notifications` (tekan lagi: `collapse`) | tidak | — |
| Screenshot | `adb exec-out screencap -p` → PNG ke `Pictures\Mirraawwrring\` + salin ke clipboard + toast | tidak | — |
| Rekam layar | proses scrcpy kedua tanpa tampilan dengan `--record` (cek nama flag "tanpa tampilan" di `--help`) | tidak | `adb shell screenrecord` per 180 detik lalu `adb pull` |
| File | buka/tutup panel file browser | tidak | — |
| Install APK | pilih file → `adb install -r` | tidak | — |
| Kirim clipboard | fokuskan jendela scrcpy lalu kirim Ctrl+V lewat `SendInput` (scrcpy menyalin clipboard PC ke HP lalu paste) | ya | — |
| Lainnya (menu) | tampilkan sentuhan (`settings put system show_touches 1/0`), layar HP mati/nyala, buka folder screenshot, salin info device | — | — |

Semua perintah `adb shell` lewat satu helper yang meng-escape argumen dengan benar (tanda kutip, spasi, karakter Unicode).

### 5.9 RemoteFileService (file browser)
- Listing: `adb shell` dengan `find <dir> -maxdepth 1 -mindepth 1 -exec stat -c '%F|%s|%Y|%n' {} +` (verifikasi format di toybox Android), diparse jadi `RemoteEntry { Name, IsDirectory, Size, Modified }`.
- Root `/sdcard`. Pintasan cepat: Download, DCIM, Pictures, Documents, Movies.
- Operasi: pull banyak file (default ke `Downloads\Mirraawwrring\`, bisa diubah), push (tombol + drag & drop dari Explorer), folder baru (`mkdir -p`), ganti nama (`mv`), hapus (dengan dialog konfirmasi, `rm -r`), filter pencarian lokal.
- File `.apk` yang di-drop ditawarkan untuk di-install.
- Progress: per file dan total byte. Progress di dalam satu file besar boleh indeterminate.

---

## 6. Layar dan alur UI

Ikuti mockup di kanvas https://claude.ai/artifact/CgXACbkFrCm9jG2R11xtyK (cadangan: `reference/mockups/`). Nama di dalam kurung adalah nama artboard/file-nya. Ringkasan:

1. **Wizard setup** (`Setup1-Deteksi`, `Setup2-Pasang`, `Setup3-Verifikasi`, `Setup3b-Gagal`, `Setup3c-HP`, `Setup4-Selesai`). Muncul otomatis jika scrcpy atau adb tidak ditemukan, dan bisa dibuka manual dari menu.
   - **Deteksi**: scrcpy, ADB, koneksi internet, dan **HP · USB debugging** (dari UsbProbe, bisa dicek sebelum adb ada).
   - **Pasang**: tiga opsi (unduh otomatis, winget, folder yang sudah ada).
   - **Verifikasi**: grup scrcpy (file lengkap, `scrcpy --version`), ADB (`adb version`, start-server, peringatan adb lain di PATH), dan HP (USB debugging + diizinkan, uji injeksi). Pengecekan HP boleh dilewati.
   - **Gagal**: tiap pemeriksaan yang gagal punya tombol perbaikan (unduh ulang; hentikan adb lain lalu ulangi dengan `"<adb lain>" kill-server`).
   - **USB debugging belum aktif**: langkah HyperOS di kiri, status HP di kanan yang diperbarui otomatis tiap 2 detik.
2. **Jendela utama** (`Main`, `Empty`): daftar perangkat (USB, Wi-Fi, dan HP terdeteksi via mDNS dengan tombol Sambung), tombol Pair wireless dan Sambung via IP, link "Aktifkan Wi-Fi dari kabel USB…", banner HyperOS jika input diblokir, pilihan mode, kualitas, dan opsi, panel log (bisa disalin), serta tombol **Mulai mirroring**. State kosong menampilkan checklist dan tombol Pair wireless sebagai aksi utama. Pengaturan terakhir diingat.
3. **Dialog Pair wireless** (`Pair`): IP:port penyambungan + kode 6 digit (kotak per digit, paste 6 digit sekaligus harus jalan). Setelah pairing sukses, tunggu koneksi otomatis 5 detik; jika tidak tersambung, minta port koneksi. QR pairing = fitur lanjutan (fase terakhir, opsional).
4. **Dialog Sambung via IP**: input `IP:port` + daftar riwayat koneksi.
5. **Toolbar samping** (`Running-Toolbar`): jendela WPF terpisah (`WindowStyle=None`, `ShowInTaskbar=false`, `Topmost=false`), **owner = HWND scrcpy** (via `WindowInteropHelper.Owner`) supaya selalu di atas scrcpy dan ikut minimize. Menempel di tepi kanan jendela scrcpy dan mengikuti pergerakan/resize lewat `SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)` yang difilter ke HWND tersebut, ditambah event minimize/restore. Kalau tidak cukup ruang di kanan layar, tempel di kiri. Konversi koordinat fisik ↔ DIP dengan benar (multi-monitor, DPI berbeda). Tutup otomatis saat proses scrcpy keluar. Tombol yang butuh injeksi diberi **titik kuning** saat InjectionProbe mendeteksi blokir; tooltip dan kliknya membuka panduan Security settings. Tooltip menampilkan shortcut bawaan scrcpy jika ada (ambil dari dokumentasi shortcut scrcpy versi yang dibundel). Hotkey screenshot milik aplikasi: **Ctrl+Shift+S**, bisa diubah, hanya aktif saat jendela scrcpy atau toolbar sedang fokus.
6. **Panel file browser** (`Running-Files`): menempel di kanan toolbar (bisa di-resize). Isinya seperti di mockup: pintasan folder, breadcrumb, aksi, daftar dengan pilihan banyak, area drop, dan progress transfer.
7. **Toast**: notifikasi kecil di dekat toolbar (screenshot tersimpan, transfer selesai), dengan tombol Buka dan Folder.

---

## 7. Tema

Warna (dari mockup):

| Token | Nilai |
|---|---|
| Background jendela | `#17191E` |
| Panel | `#1F2228` |
| Kartu / kontrol | `#262A31` |
| Border | `#2C3039`, `#343944`, `#3A3F4A` (kontrol) |
| Teks utama / sekunder / redup | `#ECEEF1` / `#C9CDD4` / `#A7ADB8` |
| Aksen | `#3DDC84`, teks di atas aksen `#0B1F13` |
| Aksen lembut | `#1C2B22` (latar terpilih), `#23402E` (badge) |
| Peringatan | ikon `#F2B84B`, teks `#F5C870`, latar `#2C2515`, border `#6B5524` |
| Error | ikon `#FF9A8B`, teks `#FFB4A8`, latar `#2E1E1C`, border `#6E3530` |
| Log | latar `#0F1114` |

- Font: `Segoe UI Variable Text`, fallback `Segoe UI`. Monospace: `Cascadia Mono`, fallback `Consolas`.
- Radius: 6 (kontrol), 8 (kartu), 10 (panel).
- Pakai **title bar native dalam mode gelap** (`DwmSetWindowAttribute` dengan `DWMWA_USE_IMMERSIVE_DARK_MODE`), bukan title bar buatan sendiri. Title bar di mockup hanya ilustrasi.
- Aksesibilitas: semua tombol ikon punya `AutomationProperties.Name` dan tooltip; navigasi keyboard dan fokus terlihat; target klik minimal 32 px.

---

## 8. Distribusi

`scripts/publish.ps1`:
- `dotnet publish src/Mirraawwrring.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- Hasil di `dist/Mirraawwrring-<versi>/Mirraawwrring.exe`.
- Switch `-BundleScrcpy`: unduh scrcpy sesuai manifest, **verifikasi SHA-256**, ekstrak ke `dist/Mirraawwrring-<versi>/scrcpy/`.
- Buat zip `dist/Mirraawwrring-<versi>-win-x64.zip`.
- Versi diambil dari `Directory.Build.props`.
- Aplikasi **tidak di-sign** (keputusan pemilik). README menjelaskan cara Unblock / "Run anyway".

---

## 9. Hal yang tidak boleh

- Jangan mengubah setelan HP tanpa aksi pengguna yang jelas.
- Jangan menghapus file di HP tanpa dialog konfirmasi.
- Jangan menjalankan `taskkill` ke semua `adb.exe` secara diam-diam. Hentikan adb lain hanya lewat tombol yang jelas.
- Jangan mengunduh binari tanpa verifikasi hash.
- Jangan mengklaim fitur sudah teruji dengan HP sungguhan. Yang butuh HP fisik masuk ke `docs/manual-test.md`.

---

## 10. Urutan kerja

Kerjakan per fase. Di akhir setiap fase: `dotnet build` tanpa error, `dotnet test` lulus, perbarui CHANGELOG, lalu commit dengan pesan bergaya conventional commits. Ringkas apa yang selesai dan apa yang perlu diuji manual, lalu **tunggu konfirmasi sebelum lanjut ke fase berikutnya**.

0. **Fondasi**: `git init`, `.gitignore` (Visual Studio/.NET), solution dan tiga proyek, `Directory.Build.props`, app manifest DPI, DI, logging ke file, ResourceDictionary tema dasar, jendela kosong bertema gelap.
1. **Core**: `IProcessRunner`, `ToolLocator`, `AdbClient` + semua parser + unit test (§11), `ScrcpyArgumentBuilder` + test, `ScrcpyLogClassifier` + test.
2. **Jendela utama**: daftar perangkat + DeviceWatcher, mode/kualitas/opsi, panel log, Mulai mirroring (ScrcpySession), banner INJECT_EVENTS, simpan pengaturan.
3. **Koneksi nirkabel**: dialog Pair wireless, Sambung via IP + riwayat, Aktifkan Wi-Fi dari USB, daftar HP via mDNS.
4. **Wizard setup**: UsbProbe, InjectionProbe, SetupService (unduh + hash + ekstrak + unblock, winget, folder), semua layar wizard termasuk gagal dan USB debugging belum aktif.
5. **Toolbar samping**: docking ke HWND scrcpy, semua tombol DeviceActions, titik kuning injeksi, toast, hotkey screenshot, rekam layar.
6. **File browser**: RemoteFileService + test parser, panel UI, drag & drop, progress, install APK.
7. **Distribusi & dokumentasi**: `publish.ps1`, README, CLAUDE.md final, `docs/manual-test.md`.
8. *(Opsional)* QR pairing, cek pembaruan scrcpy.

---

## 11. Contoh output untuk unit test

`adb devices -l` (formatnya: serial, whitespace, state, lalu pasangan `key:value`):
```
List of devices attached
192.168.1.20:41235     device product:example model:2407FPN8EG device:example transport_id:3
adb-FQYDJFRWIZGUZ989-8Usjcv._adb-tls-connect._tcp device product:example model:2407FPN8EG device:example transport_id:4
R58N12ABCDE            unauthorized usb:1-1 transport_id:2
emulator-5554          offline transport_id:1

```
Baris seperti `* daemon not running; starting now at tcp:5037` dan `* daemon started successfully` harus diabaikan.

`adb pair`:
- sukses: `Successfully paired to 192.168.1.20:37099 [guid=adb-FQYDJFRWIZGUZ989-8Usjcv]`
- gagal: `Failed: Unable to start pairing client.` atau pesan lain tanpa "Successfully paired"

`adb connect`:
- sukses: `connected to 192.168.1.20:41235`, `already connected to 192.168.1.20:41235`
- gagal: `failed to connect to 192.168.1.20:41235`, `cannot connect to 192.168.1.20:41235: ...`

Bentrok versi saat `adb start-server`:
```
adb server version (41) doesn't match this client (39); killing...
error: could not install *smartsocket* listener: Address already in use
```

Log scrcpy normal dan saat injeksi diblokir (dari HP pemilik):
```
scrcpy 4.1 <https://github.com/Genymobile/scrcpy>
INFO: ADB device found:
INFO:     --> (tcpip)  adb-FQYDJFRWIZGUZ989-8Usjcv._adb-tls-connect._tcp            device  2407FPN8EG
[server] INFO: Device: [Xiaomi] Xiaomi 2407FPN8EG (Android 16)
[server] ERROR: Injecting input events requires the caller (or the source of the instrumentation, if any) to have the INJECT_EVENTS permission.
[server] ERROR: Make sure you have enabled "USB debugging (Security Settings)" and then rebooted your device.
INFO: Renderer: direct3d11
INFO: Texture: 1220x2712
```
Dan saat tidak ada device:
```
ERROR: Could not find any ADB device
ERROR: Server connection failed
```

---

## 12. CLAUDE.md (wajib dibuat dan dirawat)

Tujuannya agar sesi Claude Code berikutnya bisa langsung bekerja tanpa membaca ulang dokumen ini. Isi minimal:
- Gambaran singkat aplikasi dan peta folder/kelas utama.
- Perintah: build, test, run, publish.
- Konvensi: MVVM, DI, async, logging, Strings.resx, aturan Core tanpa WPF.
- **Resep langkah demi langkah**:
  - Menambah tombol baru di toolbar (DeviceActions → ViewModel → XAML → tooltip/Strings → test).
  - Menambah opsi scrcpy baru (ScrcpyArgumentBuilder → settings → UI → test).
  - Menambah merek HP ke tabel VID.
  - Memperbarui versi scrcpy yang dibundel (manifest + hash dari `doc/windows.md` resmi).
- Daftar keterbatasan yang diketahui (Xiaomi Security settings, aplikasi tanpa signature).
- Selalu perbarui CLAUDE.md saat arsitektur berubah.
