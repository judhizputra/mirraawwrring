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
- [ ] Tombol Mulai mirroring berikon segitiga; setelah mirroring HP terpilih berjalan, tombol menjadi "Hentikan mirroring" berikon kotak; pilih HP lain yang tidak di-mirror → kembali segitiga.
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
- [ ] Power saat HP bangun → HP tidur (log: `sh -c 'dumpsys power | grep mWakefulness'` lalu `input keyevent 26`). Power saat HP tidur/terkunci → HP bangun ke lockscreen (log: `input keyevent 224`), toast "HP dibangunkan". Catat nilai `mWakefulness` yang terbaca di log (Awake/Asleep/Dozing) di HyperOS/Android 16.
- [ ] Mode Layar HP mati lewat Wi-Fi: biarkan melewati batas waktu layar HP (mis. 30 detik) → HP TIDAK terkunci dan mirroring tetap jalan (log scrcpy berisi `--screen-off-timeout=2147483`). Setelah mirroring dihentikan, batas waktu layar di Settings HP kembali ke nilai semula.
- [ ] Mode Layar HP mati, kunci HP dengan tombol fisik → mirror hitam → Power di toolbar membangunkan; kalau mirror masih hitam, Lainnya › Nyalakan layar HP menyalakannya (catat mana yang perlu).
- [ ] Putar layar: HP berputar 90° tiap klik (rotasi otomatis dimatikan); Buka notifikasi lalu klik lagi → panel tertutup.
- [ ] Screenshot (tombol dan Ctrl+Shift+S saat jendela scrcpy aktif): file di `Pictures\Mirraawwrring\screenshot_<waktu>.png` sesuai layar HP, Ctrl+V di Paint menempelkan gambar, toast "Screenshot disimpan" → Buka membuka gambar, Folder membuka Explorer dengan file terpilih. Hotkey tidak bereaksi saat aplikasi lain yang aktif.
- [ ] Ubah `screenshotHotkey` di `data\settings.json` (mis. "Ctrl+Alt+P"), buka ulang aplikasi → tooltip dan hotkey mengikuti.
- [ ] Rekam layar: tombol merah saat merekam; klik lagi → toast "Rekaman disimpan"; file `.mkv` di `Videos\Mirraawwrring` bisa diputar (VLC/mpv) dan berisi audio bila "Teruskan audio" aktif. Rekaman ikut berhenti saat mirroring dihentikan.
- [ ] Install APK: pilih file .apk → toast "Memasang APK…" lalu "APK terpasang"; APK rusak → toast gagal berisi pesan adb.
- [ ] Kirim clipboard ke HP: salin teks di PC, fokus ke kolom teks di HP, klik tombol → teks tertempel.
- [ ] Lainnya › Tampilkan sentuhan: lingkaran sentuh muncul di HP, item menu dicentang; klik lagi → mati. Layar HP mati: layar HP padam tapi mirroring jalan; Layar HP nyala mengembalikannya. Salin info device → tempel di Notepad berisi serial, model, Android, resolusi.
- [ ] Xiaomi tanpa Security settings: titik kuning di Power/Volume/Kembali/Home/Recent/Clipboard, legenda kuning muncul 12 detik; tooltip tombol itu berisi penjelasan; klik → jendela panduan. Volume tetap mencoba `cmd media_session volume --adj raise/lower` (catat apakah berhasil di HyperOS). Setelah Security settings diaktifkan dan mirroring dimulai ulang → titik hilang.
- [ ] Dua HP sekaligus: masing-masing sesi punya toolbar sendiri yang mengikuti jendelanya; hotkey screenshot mengambil dari HP yang jendelanya aktif.

## Fase 6 — File browser

Buka dari tombol File browser di toolbar saat mirroring.

- [ ] Panel muncul di kanan toolbar setinggi jendela scrcpy, berisi folder `/sdcard` (Android, DCIM, Download, …); chip DCIM/Download/Pictures/Documents/Movies membuka foldernya dan ditandai hijau; breadcrumb bisa diklik; klik ganda folder membukanya; ".. (induk)" naik satu tingkat.
- [ ] Ukuran dan tanggal file cocok dengan aplikasi Files di HP (format `stat` toybox: cek tidak ada baris yang hilang/salah untuk nama berisi spasi, `|`, atau huruf non-Latin).
- [ ] Folder tanpa izin (`/sdcard/Android/data`): pesan "Tidak bisa membaca folder: … Permission denied", tidak crash.
- [ ] Pilih beberapa file (checkbox / Shift+klik) → Simpan ke PC (n): progres per file dan persen total; file ada di `Downloads\Mirraawwrring`; toast dengan Folder/Buka. Pilih folder → ditarik rekursif. Batalkan di tengah transfer: berhenti, pesan "Transfer dibatalkan".
- [ ] "ubah" folder tujuan → dialog folder → tujuan baru dipakai dan tersimpan di `settings.json`.
- [ ] Kirim ke HP: pilih beberapa file di PC → progres → file muncul di folder HP (cek di aplikasi Files HP / MTP).
- [ ] Drag & drop dari Explorer ke panel: kotak drop menyala hijau; file non-APK dikirim ke folder aktif; file .apk → dialog "Install … di HP?" → Ya memasang (toast "APK terpasang"), Tidak menyalin file saja.
- [ ] Folder baru (nama berspasi) dan Ganti nama: perubahan terlihat di HP; nama tidak valid (`a/b`, kosong) ditolak dengan pesan.
- [ ] Hapus: dialog konfirmasi menyebut jumlah dan nama item; Tidak → tidak ada yang dihapus; Ya → hilang di HP (periksa juga di aplikasi Files HP).
- [ ] Pencarian lokal menyaring daftar tanpa perintah ke HP (log tidak bertambah).
- [ ] Resize lebar panel lewat tepi luar (kanan saat panel di kanan, kiri saat di kiri); lebar tetap setelah aplikasi dibuka lagi. Panel ikut berpindah ke kiri bila jendela scrcpy di tepi kanan layar.
- [ ] Tutup panel (X atau tombol toolbar) lalu buka lagi: lokasi terakhir tetap; mirroring dihentikan → panel ikut tertutup, transfer yang berjalan dibatalkan.

## Fase 7 — Paket rilis

Buat paket dengan `.\scripts\publish.ps1 -BundleScrcpy`, lalu uji di PC lain (atau akun Windows lain) yang tidak punya .NET dan scrcpy.

- [ ] Zip diekstrak ke folder biasa (mis. `D:\Apps`): SmartScreen muncul sekali → More info → Run anyway; aplikasi terbuka tanpa minta .NET.
- [ ] Tanpa Unblock di zip: `scrcpy.exe` dari bundel tetap bisa dijalankan (publish sudah Unblock-File sebelum zip); kalau Windows tetap memblokir, Unblock folder lalu catat di README.
- [ ] Folder `data\` terbentuk di samping exe; `settings.json` dan `logs\` ada di sana. Taruh di `C:\Program Files\...` → data pindah ke `%LOCALAPPDATA%\Mirraawwrring` dan log menyebut "LocalAppData".
- [ ] Paket tanpa `-BundleScrcpy`: wizard terbuka otomatis, unduhan berjalan, hash cocok, aplikasi siap.
- [ ] Seluruh alur dengan HP sungguhan (Fase 1–6) diulang memakai exe hasil publish, bukan `bin\Debug`.
- [ ] Antivirus/Defender tidak mengkarantina exe single-file (kalau ya, catat nama deteksinya).

## Fase 8 — QR pairing & pembaruan scrcpy

- [ ] Pair wireless › "Pakai QR code": QR tampil; di HP buka Wireless debugging › Pair device with QR code dan pindai → dalam ≤ 5 detik status "HP ditemukan di ip:port, pairing…" → "Tersambung: adb-…"; dialog tertutup, kartu Wi-Fi muncul. Catat apakah adb melihat layanan `_adb-tls-pairing._tcp` (log `adb mdns services`); kalau tidak pernah muncul, mDNS adb di PC ini bermasalah (firewall/Bonjour).
- [ ] Tutup dialog saat masih menunggu QR: log berhenti menampilkan `adb mdns services` tiap detik.
- [ ] Pindai QR dengan HP lain / QR kedaluwarsa: tidak ada efek; setelah 5 menit status "Belum ada HP yang memindai".
- [ ] Cek pembaruan: dengan scrcpy 4.1 dari folder aplikasi, tautan "scrcpy X tersedia" muncul di bar status beberapa detik setelah mulai (butuh internet). Klik → dialog Ya/Tidak → Ya: bar status menunjukkan persen unduhan, lalu "scrcpy X terpasang"; versi di bar status berubah; `data\scrcpy-manifest.json` berisi versi baru; mirroring masih jalan dengan versi baru (periksa log scrcpy untuk opsi yang tidak dikenali).
- [ ] Pembaruan ditolak bila mirroring berjalan ("Hentikan mirroring dulu").
- [ ] scrcpy dari winget / folder sendiri: tautan tetap muncul, tetapi dialognya hanya menawarkan membuka halaman rilis.
- [ ] Matikan "Periksa pembaruan scrcpy saat aplikasi dibuka" (wizard Selesai, atau `checkScrcpyUpdates: false`): tidak ada permintaan ke GitHub di log.

## Belum tercakup

Semua fase selesai. Item di atas yang belum dicentang adalah pekerjaan verifikasi dengan HP sungguhan.
