# Mirraawwrring

Aplikasi desktop Windows untuk menampilkan (mirror) dan mengontrol HP Android dari PC, dengan [scrcpy](https://github.com/Genymobile/scrcpy) sebagai mesinnya.

Status: **dalam pengembangan** (lihat `CHANGELOG.md`).

## Kebutuhan

- Windows 10 1809 atau lebih baru, atau Windows 11 (x64).
- scrcpy 4.1 (sudah termasuk adb). Aplikasi bisa mengunduhnya sendiri lewat wizard setup, atau memakai scrcpy yang terpasang lewat winget / folder yang sudah ada.
- HP Android dengan USB debugging aktif. Untuk Xiaomi/HyperOS, aktifkan juga **USB debugging (Security settings)** agar HP bisa dikontrol.

## Build dari sumber

```powershell
winget install Microsoft.DotNet.SDK.10
dotnet build Mirraawwrring.sln
dotnet test Mirraawwrring.sln
dotnet run --project src/Mirraawwrring.App
```

Publish portable (satu exe): `scripts/publish.ps1` (tersedia pada fase distribusi).

## Mode portable

Jika folder `data\` di samping `Mirraawwrring.exe` bisa ditulisi, pengaturan dan log disimpan di sana. Jika tidak, dipakai `%LOCALAPPDATA%\Mirraawwrring\`. Log harian disimpan 7 hari terakhir.

## Saat mirroring: toolbar samping

Setelah jendela scrcpy muncul, toolbar kecil menempel di sisi kanannya (atau kiri bila tidak muat) dan mengikuti jendela itu. Isinya: Power, volume, putar layar, Kembali/Home/Aplikasi terbaru, notifikasi, Screenshot (`Pictures\Mirraawwrring`, juga ke clipboard; hotkey `Ctrl+Shift+S` saat jendela scrcpy aktif), Rekam layar (`Videos\Mirraawwrring\*.mkv`), Install APK, Kirim clipboard ke HP, dan menu Lainnya. Di Xiaomi/HyperOS, tombol bertitik kuning butuh "USB debugging (Security settings)"; klik tombolnya untuk melihat caranya.

Hotkey screenshot bisa diubah di `data\settings.json` (`"screenshotHotkey": "Ctrl+Alt+P"`; modifier Ctrl/Alt/Shift/Win + huruf, angka, F1–F24, atau nama tombol seperti PrintScreen).

## Aplikasi tidak di-sign

Exe tidak ditandatangani (keputusan pemilik). Saat pertama dijalankan, Windows SmartScreen mungkin menampilkan peringatan:

1. Klik **More info** / **Info selengkapnya**.
2. Klik **Run anyway** / **Tetap jalankan**.

Jika file hasil unduhan diblokir, klik kanan `Mirraawwrring.exe` → **Properties** → centang **Unblock** → **OK**.

## Struktur repo

```
src/Mirraawwrring.Core/    logika (adb, scrcpy, parser, layanan), tanpa WPF
src/Mirraawwrring.App/     WPF: Views, ViewModels, tema, interop jendela
tests/Mirraawwrring.Core.Tests/
config/scrcpy-manifest.json   versi scrcpy yang di-pin + URL + SHA-256
reference/                 prototipe PowerShell, mockup desain, output scrcpy --help
docs/manual-test.md        checklist uji manual dengan HP sungguhan
```

Peta arsitektur dan resep menambah fitur ada di `CLAUDE.md`.
