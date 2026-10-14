# Mockup Mirraawwrring

Salinan dari kanvas desain https://claude.ai/artifact/CgXACbkFrCm9jG2R11xtyK (per 14 Oktober 2026).
Kalau berbeda, versi di link itu yang terbaru.

Format file: `.dc.html` adalah format kanvas desain. Abaikan `<x-dc>`, `<helmet>`, `<sc-*>`, `{{...}}`,
`<script src="./support.js">`, dan blok `<script type="text/x-dc">`. Yang penting adalah tata letak, inline
style (warna, ukuran, spasi, radius), teks UI, dan state yang digambarkan. Title bar di mockup hanya ilustrasi
(aplikasi memakai title bar native mode gelap). Nama "scrcpy Launcher" di mockup = nama kerja lama, ganti
menjadi "Mirraawwrring".

| File | Layar | Ukuran |
|---|---|---|
| `Main.dc.html` | Jendela utama, HP terhubung (daftar perangkat, mode, kualitas, opsi, log, Mulai mirroring) | 1000×700 |
| `Empty.dc.html` | Jendela utama, belum ada HP terdeteksi | 1000×700 |
| `Pair.dc.html` | Dialog pair wireless (IP:port + kode 6 digit, port koneksi cadangan) | 520×640 |
| `Setup1-Deteksi.dc.html` | Wizard 1: deteksi scrcpy, ADB, internet, HP · USB debugging | 640×560 |
| `Setup2-Pasang.dc.html` | Wizard 2: unduh otomatis / winget / folder yang ada | 640×560 |
| `Setup3-Verifikasi.dc.html` | Wizard 3: verifikasi scrcpy, ADB, HP | 640×560 |
| `Setup3b-Gagal.dc.html` | Wizard 3: verifikasi gagal + tombol perbaikan | 640×560 |
| `Setup3c-HP.dc.html` | Wizard 3: USB debugging belum aktif (langkah HyperOS + status live) | 640×560 |
| `Setup4-Selesai.dc.html` | Wizard 4: selesai, pilih sambung via USB atau tanpa kabel | 640×560 |
| `Running-Toolbar.dc.html` | Saat mirroring: toolbar samping menempel di jendela scrcpy, tooltip, toast, penanda injeksi diblokir | 700×920 |
| `Running-Files.dc.html` | Saat mirroring: panel file browser di samping toolbar | 1000×920 |
