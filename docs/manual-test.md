# Checklist uji manual (butuh HP sungguhan)

Perangkat uji utama: Xiaomi 2407FPN8EG, HyperOS, Android 16. Centang setelah diuji; catat versi scrcpy dan tanggal.

## Fase 1 — Core (dijalankan lewat aplikasi mulai Fase 2, atau lewat log startup)

- [ ] Startup mencatat lokasi scrcpy dan adb di `data\logs\Mirraawwrring-<tanggal>.log` (lihat baris "scrcpy: … adb: …").
- [ ] Jika Android SDK platform-tools ada di PATH, log memperingatkan "adb lain terdeteksi".
- [ ] scrcpy terpasang lewat winget: symlink `%LOCALAPPDATA%\Microsoft\WinGet\Links\scrcpy.exe` di-resolve ke folder paket dan adb di folder itu yang dipakai.
- [ ] `adb devices -l` nyata dengan HP USB: serial, model, dan state (`device` / `unauthorized`) terbaca benar.
- [ ] HP via Wireless debugging (serial `adb-…._adb-tls-connect._tcp`) terdeteksi sebagai Wi-Fi (mDNS).
- [ ] HP via `adb connect IP:port` terdeteksi sebagai Wi-Fi (IP).
- [ ] `adb mdns services` menampilkan layanan `_adb-tls-connect._tcp` saat Wireless debugging aktif.
- [ ] `getprop` mengembalikan manufacturer "Xiaomi", model "2407FPN8EG", Android "16".
- [ ] Saat HyperOS memblokir injeksi, baris `[server] ERROR: … INJECT_EVENTS …` dari scrcpy dikenali sebagai `InjectionBlocked`.

## Fase 2 — Jendela utama

Siapkan: folder `scrcpy\` (hasil ekstrak scrcpy-win64-v4.1) di samping `Mirraawwrring.exe`, atau scrcpy dari winget.

- [ ] Tanpa HP: state kosong tampil (checklist "Periksa di HP", tombol Pair wireless hijau), panel Mode redup, status "menunggu perangkat", versi scrcpy tampil di bar status.
- [ ] Colok HP USB dengan USB debugging aktif tapi belum diizinkan: kartu muncul dengan badge "Belum diizinkan" dalam ≤ 2 detik; setelah Izinkan di HP badge menjadi "Terhubung", nama berubah menjadi "Xiaomi 2407FPN8EG" dan baris "Android 16" muncul.
- [ ] Satu HP siap → otomatis terpilih, panel Mode aktif, tombol Mulai mirroring hijau.
- [ ] Mulai mirroring: jendela scrcpy berjudul "Mirraawwrring - <model>" muncul tanpa jendela konsol; log scrcpy masuk ke panel Log; badge kartu menjadi "Mirroring"; tombol berubah "Hentikan mirroring".
- [ ] Hentikan mirroring menutup jendela scrcpy; badge kembali "Terhubung".
- [ ] Tutup jendela scrcpy dari tombol X-nya: badge dan tombol kembali normal (sesi terdeteksi berakhir).
- [ ] Mode Lihat saja: tidak bisa klik/ketik di jendela scrcpy. Mode Layar HP mati: layar HP padam, tampilan di PC tetap. Mode Hemat data: log scrcpy menunjukkan `--max-size=1024 --video-bit-rate=4M --max-fps=30`.
- [ ] Rekam ke file: file `Videos\Mirraawwrring\scrcpy_<tanggal>.mp4` ada dan bisa diputar setelah sesi ditutup; "Buka folder rekaman" membuka foldernya.
- [ ] Xiaomi tanpa "USB debugging (Security settings)": banner kuning "Kontrol diblokir HyperOS" muncul saat scrcpy melaporkan INJECT_EVENTS; "Lihat langkahnya" membuka jendela panduan.
- [ ] Input UHID dicentang: kontrol jalan walau Security settings mati; mouse PC tertangkap jendela scrcpy, Alt kiri melepasnya.
- [ ] Ubah mode/kualitas/opsi lalu tutup dan buka aplikasi: pilihan tetap (lihat `data\settings.json`).
- [ ] Restart ADB (state kosong): log menunjukkan `adb kill-server` lalu `adb start-server`, daftar perangkat muncul lagi.
- [ ] Dua HP sekaligus: masing-masing bisa dimulai sebagai sesi terpisah.
- [ ] Tutup aplikasi saat mirroring berjalan: jendela scrcpy ikut tertutup.
- [ ] Salin log → tempel di Notepad berisi baris-baris panel Log.

## Belum tercakup (fase berikutnya)

Pair wireless, Sambung via IP, wizard setup, toolbar, file browser: ditambahkan saat fasenya selesai.
