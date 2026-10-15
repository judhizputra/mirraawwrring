# Mirraawwrring

Aplikasi desktop Windows untuk menampilkan (mirror) dan mengontrol HP Android dari PC, dengan [scrcpy](https://github.com/Genymobile/scrcpy) sebagai mesinnya. Satu exe portable, tema gelap, bahasa Indonesia.

Fitur:

- **Wizard setup**: mendeteksi scrcpy/ADB/internet/HP, mengunduh scrcpy resmi (SHA-256 diverifikasi) atau memakai winget / folder yang sudah ada, memverifikasi semuanya berjalan, dan memandu mengaktifkan USB debugging (termasuk langkah khusus Xiaomi/HyperOS).
- **Jendela utama**: daftar HP (USB, Wi-Fi, dan HP di jaringan via mDNS), Pair wireless dengan kode 6 digit atau QR code, Sambung via IP dengan riwayat, Aktifkan Wi-Fi dari kabel USB, mode (mirror + kontrol, lihat saja, layar HP mati, hemat data), kualitas, opsi scrcpy, panel log.
- **Toolbar samping** yang menempel di jendela scrcpy: Power, volume, putar layar, Kembali/Home/Aplikasi terbaru, notifikasi, screenshot (hotkey `Ctrl+Shift+S`), rekam layar, file browser, install APK, kirim clipboard, menu lainnya.
- **File browser**: jelajahi penyimpanan HP, simpan ke PC, kirim ke HP (termasuk drag & drop), folder baru, ganti nama, hapus dengan konfirmasi.

## Kebutuhan

- Windows 10 1809 atau lebih baru, atau Windows 11, x64. Tidak perlu memasang .NET (sudah termasuk di exe).
- scrcpy 4.1 (sudah termasuk ADB). Ada tiga cara: paket `-BundleScrcpy` sudah berisi folder `scrcpy\`, atau wizard setup mengunduhnya, atau pakai scrcpy dari winget / folder sendiri.
- HP Android 5+ dengan USB debugging aktif (Wireless debugging butuh Android 11+). Untuk Xiaomi/HyperOS, aktifkan juga **USB debugging (Security settings)** agar HP bisa dikontrol; tanpa itu tombol kontrol diberi titik kuning dan hanya mirror yang jalan.

## Memakai paket rilis

1. Unduh `Mirraawwrring-<versi>-win-x64.zip`, klik kanan → **Properties** → centang **Unblock** → **OK** (supaya semua file di dalamnya tidak dianggap "dari internet"), lalu ekstrak ke folder mana saja, misalnya `D:\Apps\Mirraawwrring`.
2. Jalankan `Mirraawwrring.exe`. Lihat bagian [Aplikasi tidak di-sign](#aplikasi-tidak-di-sign) jika SmartScreen muncul.
3. Jika folder `scrcpy\` belum ada di samping exe, wizard setup terbuka otomatis dan menawarkan unduhan (±11 MB).
4. Colok HP (USB debugging aktif, izinkan popup di HP) atau pakai **Pair wireless**, lalu **Mulai mirroring**.

Isi folder setelah dipakai:

```
Mirraawwrring.exe          aplikasi (self-contained, single-file)
config\scrcpy-manifest.json  versi scrcpy yang di-pin + URL + SHA-256
scrcpy\                    scrcpy + adb (bundel atau hasil unduhan wizard)
data\settings.json         pengaturan (mode portable)
data\logs\                 log harian, 7 hari terakhir
```

Hasil kerja disimpan di `Pictures\Mirraawwrring` (screenshot), `Videos\Mirraawwrring` (rekaman), dan `Downloads\Mirraawwrring` (file dari HP, bisa diubah).

## Mode portable

Jika folder `data\` di samping `Mirraawwrring.exe` bisa ditulisi, pengaturan dan log disimpan di sana. Jika tidak (mis. di `Program Files`), dipakai `%LOCALAPPDATA%\Mirraawwrring\`. Pindahkan seluruh folder untuk membawa pengaturan ikut serta.

## Build dari sumber

```powershell
winget install Microsoft.DotNet.SDK.10
dotnet build Mirraawwrring.sln
dotnet test Mirraawwrring.sln
dotnet run --project src/Mirraawwrring.App
```

Saat dijalankan dari `bin\`, letakkan folder `scrcpy\` (isi `scrcpy-win64-v4.1.zip`) di samping exe atau biarkan wizard mengunduhnya.

### Publish (paket rilis)

```powershell
.\scripts\publish.ps1                 # dist\Mirraawwrring-<versi>\ + zip, scrcpy diunduh wizard saat pertama jalan
.\scripts\publish.ps1 -BundleScrcpy   # sekaligus mengunduh scrcpy sesuai manifest, memverifikasi SHA-256, dan menaruhnya di scrcpy\
.\scripts\publish.ps1 -SkipTests -NoZip
```

Skrip membaca versi dari `Directory.Build.props`, menjalankan unit test, lalu `dotnet publish` win-x64 self-contained single-file dengan kompresi. Hasil: `dist\Mirraawwrring-<versi>\Mirraawwrring.exe` (±60 MB) dan `dist\Mirraawwrring-<versi>-win-x64.zip`. Jika SHA-256 unduhan scrcpy tidak cocok, skrip berhenti dan tidak membundel apa pun.

## Saat mirroring: toolbar samping

Setelah jendela scrcpy muncul, toolbar kecil menempel di sisi kanannya (atau kiri bila tidak muat) dan mengikuti jendela itu, termasuk saat minimize. Tooltip menampilkan pintasan bawaan scrcpy (Alt+P, Alt+H, …). Screenshot disimpan ke `Pictures\Mirraawwrring` dan disalin ke clipboard; hotkey `Ctrl+Shift+S` hanya aktif saat jendela scrcpy atau toolbar yang aktif, dan bisa diubah di `data\settings.json` (`"screenshotHotkey": "Ctrl+Alt+P"`; modifier Ctrl/Alt/Shift/Win + huruf, angka, F1–F24, atau nama tombol seperti PrintScreen). Rekam layar dari toolbar menghasilkan `.mkv` di `Videos\Mirraawwrring` (opsi "Rekam ke file" di jendela utama menghasilkan `.mp4`).

Di Xiaomi/HyperOS tanpa "USB debugging (Security settings)", tombol yang butuh injeksi input diberi titik kuning; klik tombolnya untuk melihat caranya. Alternatif: centang **Input UHID** di Opsi (mouse PC akan ditangkap jendela scrcpy, Alt kiri melepasnya).

## File browser

Tombol folder di toolbar membuka panel file HP di sampingnya (lebarnya bisa ditarik). Pintasan Download/DCIM/Pictures/Documents/Movies, pilih banyak file lalu "Simpan ke PC" (default `Downloads\Mirraawwrring`, bisa diubah), "Kirim ke HP" atau tarik file dari Explorer ke panel (file `.apk` ditawarkan untuk di-install), folder baru, ganti nama, hapus (selalu dengan konfirmasi), dan pencarian di folder aktif.

## Pembaruan scrcpy

Saat dibuka, aplikasi membandingkan versi scrcpy yang dipakai dengan rilis terbaru di GitHub (bisa dimatikan lewat centang di layar terakhir wizard atau `"checkScrcpyUpdates": false`). Jika ada versi baru, tautan "scrcpy X tersedia" muncul di bar status. Pemasangan otomatis hanya dilakukan bila SHA-256 resmi ditemukan di dokumentasi rilis scrcpy dan scrcpy yang dipakai berada di folder `scrcpy\` aplikasi; selain itu aplikasi hanya membuka halaman rilis. Opsi scrcpy di aplikasi ini diverifikasi untuk 4.1; setelah memperbarui, periksa panel Log kalau ada opsi yang tidak dikenali.

## Masalah umum

- **"Server ADB gagal berjalan" / port 5037 bentrok**: ada adb versi lain (Android Studio, platform-tools di PATH). Wizard Verifikasi menawarkan tombol "Hentikan & ulangi" yang menjalankan `"<adb lain>" kill-server`; Mirraawwrring selalu memakai adb dari folder scrcpy-nya sendiri.
- **HP tidak muncul**: pastikan kabel data (bukan hanya pengisian), USB debugging aktif, dan popup "Allow USB debugging" di HP disetujui. Wizard Deteksi menampilkan status HP secara live.
- **Kontrol tidak jalan di Xiaomi**: aktifkan USB debugging (Security settings) (butuh akun Xiaomi + data seluler) atau pakai Input UHID.
- **Log**: panel Log di jendela utama (bisa disalin) atau `data\logs\Mirraawwrring-<tanggal>.log`.

## Aplikasi tidak di-sign

Exe tidak ditandatangani (keputusan pemilik). Saat pertama dijalankan, Windows SmartScreen mungkin menampilkan peringatan:

1. Klik **More info** / **Info selengkapnya**.
2. Klik **Run anyway** / **Tetap jalankan**.

Jika file hasil unduhan diblokir, klik kanan `Mirraawwrring.exe` → **Properties** → centang **Unblock** → **OK**. Memakai `Unblock-File` di PowerShell juga bisa: `Get-ChildItem -Recurse | Unblock-File`.

## Struktur repo

```
src/Mirraawwrring.Core/    logika (adb, scrcpy, parser, layanan, file browser), tanpa WPF
src/Mirraawwrring.App/     WPF: Views, ViewModels, tema, interop jendela
tests/Mirraawwrring.Core.Tests/   xUnit (parser dari output nyata, layanan dengan adb palsu)
scripts/publish.ps1        paket rilis portable (+ bundel scrcpy terverifikasi)
config/scrcpy-manifest.json   versi scrcpy yang di-pin + URL + SHA-256
reference/                 prototipe PowerShell, mockup desain, output scrcpy --help
docs/manual-test.md        checklist uji manual dengan HP sungguhan
```

Peta arsitektur dan resep menambah fitur ada di `CLAUDE.md`; riwayat perubahan di `CHANGELOG.md`.
