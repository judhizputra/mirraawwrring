# Cara memakai paket prompt Mirraawwrring

## 1. Siapkan di Windows
Jalankan di PowerShell (Windows, bukan WSL, karena aplikasi WPF harus di-build dan dijalankan di Windows):

```
winget install Microsoft.DotNet.SDK.10
winget install Git.Git
```

Pastikan Claude Code sudah terpasang dan login di Windows.

## 2. Siapkan folder proyek
1. Buat folder, misalnya `D:\Projects\Mirraawwrring`.
2. Ekstrak isi paket ini ke folder itu, sehingga ada `PROMPT.md` dan folder `reference\` di dalamnya.

## 3. Jalankan Claude Code
Di PowerShell, masuk ke folder proyek lalu jalankan `claude`. Ketik:

> Baca PROMPT.md dan semua isi folder reference, lalu kerjakan Fase 0 dan Fase 1. Berhenti setelah Fase 1 selesai dan ringkas hasilnya.

Setelah dicek, lanjutkan per fase:

> Lanjut Fase 2.

Mengerjakan per fase membuat hasilnya lebih mudah dicek. Setiap fase di-commit ke git, jadi bisa dikembalikan kalau ada yang salah.

## 4. Pengembangan berikutnya
Claude Code akan membuat `CLAUDE.md` berisi peta kode dan resep menambah fitur. Untuk pengembangan baru, cukup buka `claude` di folder proyek dan jelaskan fiturnya, misalnya:

> Tambahkan tombol di toolbar untuk membuka aplikasi kamera di HP.

## Catatan
- Link desain di PROMPT.md sudah publik. Kalau Claude Code tetap tidak bisa membacanya, ia akan memakai salinan di `reference\mockups\`.
- Bagian yang butuh HP sungguhan (USB debugging, Xiaomi Security settings, toolbar) perlu kamu uji sendiri. Daftarnya ada di `docs\manual-test.md` yang akan dibuat Claude Code.
