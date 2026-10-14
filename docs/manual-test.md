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

## Fase 3 — Koneksi nirkabel

HP dan PC di Wi-Fi yang sama. Di HP: Opsi pengembang › Wireless debugging.

- [ ] Wireless debugging aktif (belum pernah pair ke PC ini): tidak ada kartu "HP di jaringan" (adb hanya melihat HP yang sudah di-pair).
- [ ] Pair wireless: ketik alamat dari dialog "Pair device with pairing code", paste kode 6 digit ke kotak pertama (Ctrl+V) → semua kotak terisi; tekan Pair & sambungkan → status "Pairing…" lalu "Tersambung: adb-…" dalam ≤ 5 detik; dialog menutup; kartu HP muncul sebagai "Wi-Fi · Wireless debugging" dan terpilih.
- [ ] Pair dengan kode salah: pesan gagal merah berisi output adb + petunjuk buka ulang dialog di HP.
- [ ] Jika koneksi otomatis tidak terjadi: langkah 1 jadi nonaktif, langkah 2 aktif, status meminta port koneksi; masukkan port dari layar utama Wireless debugging → Sambungkan → tersambung.
- [ ] Matikan lalu nyalakan Wireless debugging di HP yang sudah di-pair: kartu "HP di jaringan · ip:port" muncul dalam ≤ 5 detik dengan tombol Sambung; klik → HP tersambung dan kartu mDNS hilang.
- [ ] Sambung via IP: alamat dari layar Wireless debugging (ip:port) → tersambung; alamat tercatat di Riwayat; buka dialog lagi → riwayat tampil, klik mengisi input, "Hapus dari riwayat" menghapusnya; isi tanpa port → dipakai :5555.
- [ ] Sambung via IP ke alamat salah: pesan error merah dari adb, riwayat tidak bertambah.
- [ ] Aktifkan Wi-Fi dari kabel USB (HP tersambung USB, Wi-Fi HP menyala): pesan "Wi-Fi aktif. Tersambung ke ip:5555"; kartu Wi-Fi muncul; cabut kabel USB → kartu USB hilang, kartu Wi-Fi tetap; mirroring lewat Wi-Fi berjalan.
- [ ] Aktifkan Wi-Fi tanpa HP USB: pesan "Sambungkan HP lewat kabel USB dulu".

## Fase 4 — Wizard setup

Untuk memaksa wizard: hapus/ganti nama folder `scrcpy\` di samping exe (dan pastikan scrcpy tidak ada di PATH), lalu jalankan aplikasi.

- [ ] Tanpa scrcpy: wizard muncul otomatis di atas jendela utama; baris scrcpy "Tidak ditemukan", ADB sesuai kondisi PATH, internet "Tersedia".
- [ ] Colok HP dengan USB debugging MATI: dalam ≤ 2 detik baris "HP · USB debugging" menjadi "Belum aktif" dengan deskripsi merek (mis. "Xiaomi tersambung USB…"); nyalakan USB debugging → berubah menjadi "Belum diizinkan"/"Siap" tanpa menekan Periksa ulang.
- [ ] "Cara mengaktifkan" membuka layar langkah HyperOS; status HP di kanan berubah live; "Lewati" kembali ke Deteksi.
- [ ] Pasang › Unduh otomatis: progress MB berjalan, "Batalkan unduhan" menghentikan dan tombol kembali ke "Pasang"; unduhan penuh → verifikasi hash → ekstrak ke `scrcpy\` → file tidak lagi "diblokir" (klik kanan › Properties tanpa tombol Unblock) → otomatis ke Verifikasi.
- [ ] Pasang › winget: output winget tampil di kotak; setelah selesai, lokasi scrcpy = symlink winget yang di-resolve.
- [ ] Pasang › Pakai folder yang sudah ada: folder tanpa `scrcpy-server` ditolak dengan daftar file hilang; folder valid → Pasang → Verifikasi; `settings.json` berisi `customScrcpyPath`.
- [ ] Verifikasi dengan HP tersambung & diizinkan: semua baris hijau, "Uji kontrol" hijau (Security settings aktif) atau kuning dengan tombol "Lihat caranya" (HyperOS memblokir); otomatis ke Selesai.
- [ ] Verifikasi tanpa HP: grup HP gagal → otomatis ke layar USB debugging; colok HP + izinkan → tombol Lanjut aktif → Verifikasi ulang lolos.
- [ ] Bentrok adb (jalankan `adb start-server` dari Android SDK yang versinya berbeda dulu): baris "Server ADB berjalan" gagal dengan tombol "Hentikan & ulangi"; tombol menjalankan `"<adb lain>" kill-server` lalu verifikasi ulang lolos.
- [ ] Karantina scrcpy.exe (ganti nama sementara): "scrcpy tidak bisa dijalankan" dengan tombol "Unduh ulang" yang kembali ke Pasang.
- [ ] Selesai: versi 4.1, lokasi folder; "Tanpa kabel" menutup wizard dan membuka dialog Pair; "Buka Mirraawwrring" menutup wizard, status bar jendela utama menjadi "adb siap"/"menunggu perangkat" dengan "scrcpy 4.1".
- [ ] Tombol "Setup…" di bar status membuka wizard lagi kapan saja.

## Fase 5 — Toolbar samping

Mulai mirroring dari jendela utama; toolbar muncul di kanan jendela scrcpy setelah jendelanya tampil.

- [ ] Toolbar menempel di kanan jendela scrcpy, sejajar dengan tepi atas area gambar (bukan title bar); geser/resize jendela scrcpy → toolbar mengikuti tanpa lag berarti; dekatkan ke tepi kanan layar → toolbar pindah ke kiri.
- [ ] Monitor kedua dengan skala DPI berbeda: toolbar tetap menempel rapat dan ukurannya proporsional saat jendela scrcpy dipindah antar monitor.
- [ ] Minimize scrcpy → toolbar ikut hilang; restore → muncul lagi. Tutup jendela scrcpy / Hentikan mirroring → toolbar hilang.
- [ ] Klik tombol toolbar tidak memindahkan fokus keyboard dari jendela scrcpy (ketik di scrcpy tetap masuk ke HP setelah menekan tombol).
- [ ] Power, Volume naik/turun, Kembali, Home, Aplikasi terbaru: HP merespons; tooltip menampilkan pintasan (Alt+P, Alt+↑, …).
- [ ] Putar layar: HP berputar 90° tiap klik (rotasi otomatis dimatikan); Buka notifikasi lalu klik lagi → panel tertutup.
- [ ] Screenshot (tombol dan Ctrl+Shift+S saat jendela scrcpy aktif): file di `Pictures\Mirraawwrring\screenshot_<waktu>.png` sesuai layar HP, Ctrl+V di Paint menempelkan gambar, toast "Screenshot disimpan" → Buka membuka gambar, Folder membuka Explorer dengan file terpilih. Hotkey tidak bereaksi saat aplikasi lain yang aktif.
- [ ] Ubah `screenshotHotkey` di `data\settings.json` (mis. "Ctrl+Alt+P"), buka ulang aplikasi → tooltip dan hotkey mengikuti.
- [ ] Rekam layar: tombol merah saat merekam; klik lagi → toast "Rekaman disimpan"; file `.mkv` di `Videos\Mirraawwrring` bisa diputar (VLC/mpv) dan berisi audio bila "Teruskan audio" aktif. Rekaman ikut berhenti saat mirroring dihentikan.
- [ ] Install APK: pilih file .apk → toast "Memasang APK…" lalu "APK terpasang"; APK rusak → toast gagal berisi pesan adb.
- [ ] Kirim clipboard ke HP: salin teks di PC, fokus ke kolom teks di HP, klik tombol → teks tertempel.
- [ ] Lainnya › Tampilkan sentuhan: lingkaran sentuh muncul di HP, item menu dicentang; klik lagi → mati. Layar HP mati: layar HP padam tapi mirroring jalan; Layar HP nyala mengembalikannya. Salin info device → tempel di Notepad berisi serial, model, Android, resolusi.
- [ ] Xiaomi tanpa Security settings: titik kuning di Power/Volume/Kembali/Home/Recent/Clipboard, legenda kuning muncul 12 detik; tooltip tombol itu berisi penjelasan; klik → jendela panduan. Volume tetap mencoba `cmd media_session volume --adj raise/lower` (catat apakah berhasil di HyperOS). Setelah Security settings diaktifkan dan mirroring dimulai ulang → titik hilang.
- [ ] Dua HP sekaligus: masing-masing sesi punya toolbar sendiri yang mengikuti jendelanya; hotkey screenshot mengambil dari HP yang jendelanya aktif.

## Belum tercakup (fase berikutnya)

File browser: ditambahkan saat fasenya selesai.
