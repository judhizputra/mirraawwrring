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

## Belum tercakup (fase berikutnya)

Mulai mirroring, pair wireless, wizard setup, toolbar, file browser: ditambahkan saat fasenya selesai.
