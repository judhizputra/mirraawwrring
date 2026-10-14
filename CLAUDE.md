# CLAUDE.md — peta proyek Mirraawwrring

Aplikasi desktop Windows (WPF, .NET 10, C#) untuk mirror dan kontrol HP Android lewat **scrcpy**. Spesifikasi lengkap ada di `PROMPT.md`; file ini adalah ringkasan operasional supaya sesi berikutnya bisa langsung bekerja. **Perbarui file ini setiap kali arsitektur berubah.**

Status fase (lihat `PROMPT.md` §10 dan `CHANGELOG.md`): Fase 0–6 selesai. Berikutnya: Fase 7 (distribusi & dokumentasi: `scripts/publish.ps1` portable single-file + opsi `-BundleScrcpy`, README final, CLAUDE.md final, `docs/manual-test.md`). Fase 8 opsional: QR pairing, cek pembaruan scrcpy.

## Perintah

```powershell
dotnet build Mirraawwrring.sln              # build semua
dotnet test Mirraawwrring.sln               # unit test Core (xUnit)
dotnet run --project src/Mirraawwrring.App  # jalankan aplikasi
scripts/publish.ps1 [-BundleScrcpy]         # publish portable (Fase 7)
```

Build Core memakai `TreatWarningsAsErrors`; warning di Core = build gagal.

## Peta folder

```
src/Mirraawwrring.Core/        logika, TANPA WPF (target net10.0-windows, boleh Win32/WMI)
  AppPaths.cs                  semua path (exe, data portable, logs, scrcpy bundel, Videos/Pictures/Downloads)
  ServiceCollectionExtensions  AddMirraawwrringCore(): daftar semua layanan Core ke DI
  Logging/                     LogEntry, LogBuffer (panel Log), FileLogWriter (file harian, 7 hari), AppLoggerProvider
  Processes/                   IProcessRunner + ProcessRunner (async, timeout, cancel, stdout/stderr/exit, binary stdout)
  IO/IFileSystem.cs            abstraksi file system untuk ToolLocator (fake di test)
  Tools/                       ToolLocator (cari scrcpy/adb §5.1), ToolLocation, ToolRegistry (lokasi aktif), ToolNotFoundException
  Adb/                         Device model, AdbDevicesParser, AdbResultParsers (pair/connect/server/mdns/wifi-ip),
                               ShellQuoting, IAdbClient + AdbClient, DeviceWatcher (polling 2 s)
  Scrcpy/                      ScrcpyOptions + MirrorMode, ScrcpyArgumentBuilder (opsi → argumen), ScrcpyLogClassifier,
                               ScrcpySession (satu proses scrcpy: log terklasifikasi, InjectionBlocked, HWND), ScrcpySessionManager (banyak sesi, env ADB, --version)
  Settings/                    AppSettings (model settings.json), SettingsStore (load/save atomic)
  Wireless/                    AddressUtil (validasi host:port, kode), WirelessService (pair/connect/tcpip + riwayat), MdnsWatcher (polling mdns 5 s)
  Usb/                         UsbVendorTable (VID → merek), UsbProbeParser (murni) + WmiUsbProbe (WMI) + UsbWatcher, PhoneStatusEvaluator (USB + adb → PhoneUsbStatus)
  Devices/InjectionProbe       uji `input keyevent 0`, cache per serial
  Devices/DeviceActions        SEMUA perintah tombol toolbar (§5.8): keyevent, volume + cadangan, rotasi, notifikasi, show_touches,
                               screenshot (exec-out screencap → Pictures), install APK, info device; tabel RequiresInjection; Chords (Ctrl+V, Alt+O)
  Input/Hotkey                 parser "Ctrl+Shift+S" → HotkeyModifiers (= MOD_*) + virtual key (murni)
  Files/                       RemoteEntry + RemotePath (path POSIX, breadcrumb, pintasan, validasi nama), RemoteListingParser (murni: find -exec stat),
                               RemoteFileService (list, pull/push + TransferProgress, mkdir/mv/rm; konfirmasi hapus di UI)
  Setup/                       ScrcpyManifest, SetupService (unduh/hash/ekstrak/unblock, winget, validasi folder, cek internet), VerificationService (7 pemeriksaan + FixAction)
  Interop/                     NativeMethods (EnumWindows dsb.), WindowFinder (cari HWND jendela scrcpy dari PID),
                               DockPlacement (posisi toolbar kanan/kiri scrcpy dalam piksel fisik, murni)
src/Mirraawwrring.App/         WPF
  App.xaml(.cs)                DI container, logging, handler exception global, LocateTools() saat startup (pakai CustomScrcpyPath dari settings)
  Themes/Colors.xaml           token warna/font/radius dari mockup (§7). Jangan tulis hex di View.
  Themes/Icons.xaml            Geometry ikon garis (Icon.*) + Style.Icon
  Themes/Controls.xaml         style: Style.Window, Style.Button(.Accent/.Icon/.Subtle), Style.TextBox, Style.CheckBox, Style.Panel, Style.Card, ToolTip, ScrollBar
  Themes/Controls.Main.xaml    Style.ComboBox, Style.ModeCard (RadioButton kartu), Style.DeviceList/DeviceItem, Style.Badge
  Converters/Converters.cs     EnumToBool (RadioButton↔enum), InvertedBoolToVisibility, NullToCollapsed
  Views/MainWindow             jendela utama (mockup Main/Empty); HyperOsGuideWindow = panduan Security settings
  Views/PairWirelessWindow     dialog Pair (mockup Pair); logika 6 kotak digit di code-behind
  Views/ConnectIpWindow        dialog Sambung via IP + riwayat
  ViewModels/MainViewModel     daftar perangkat + kandidat mDNS, mode/kualitas/opsi (disimpan ke settings), log, status, Mulai/Hentikan mirroring,
                               event ke View: PairWirelessRequested, ConnectIpRequested, HyperOsGuideRequested, MessageRequested
  ViewModels/DeviceItemViewModel  satu kartu perangkat (subtitle, badge, IsMirroring); MdnsCandidateViewModel = kartu "HP di jaringan"
  ViewModels/PairWirelessViewModel, ConnectIpViewModel  dialog nirkabel (event Completed → View menutup dialog)
  ViewModels/Setup/            SetupViewModel (navigasi + footer per langkah) dan halaman: DetectPage, InstallPage, VerifyPage, PhonePage, DonePage
  Views/SetupWindow            wizard (DataTemplate per tipe halaman, stepper Grid); dibuka dari MainWindow.OnLoaded jika tools tidak lengkap atau tombol "Setup…"
  Themes/Controls.Setup.xaml   Style.ProgressBar, Style.OptionCard, Style.Button.Danger, Style.ChoiceCard
  Themes/Controls.Toolbar.xaml Style.Button.Toolbar (40x40), Style.Icon.Toolbar, Style.InjectionDot, Style.Toast, Style.ToolbarLegend, Style.ContextMenu/MenuItem
  Themes/Controls.Files.xaml   Style.Chip, Style.Button.Small(.Icon), Style.Button.Accent.Small, Style.Button.Crumb, Style.FileList/FileRow, Style.ResizeThumb
  Views/FilesPanel             UserControl panel file (mockup Running-Files) di kolom samping ToolbarWindow; dialog file/folder/konfirmasi dipasang ke VM di code-behind; drag & drop
  ViewModels/FilesPanelViewModel  lokasi/breadcrumb/pintasan, daftar + filter lokal + pilihan (RemoteEntryViewModel.IsSelected), editor nama inline, pull/push/drop/hapus, progres, toast via event
  Views/ToolbarWindow          toolbar samping per sesi scrcpy: owner = HWND scrcpy, WS_EX_NOACTIVATE, mengikuti lewat WinEventHook, RegisterHotKey screenshot,
                               Reposition() = WindowGeometry + DockPlacement; menu "Lainnya" = ContextMenu; dibuat MainWindow.OnToolbarRequested
  ViewModels/ToolbarViewModel  semua command tombol (memanggil DeviceActions), InjectionBlocked (RecheckAsync tiap sesi + log scrcpy) → tooltip/titik kuning/legenda,
                               rekam headless (ScrcpySessionManager.StartHeadlessRecording), toast (ToastItem), PickApkFile (Func dari View), GuideRequested
  Interop/                     NativeMethods (P/Invoke UI: DWM, geometri, SetWinEventHook, RegisterHotKey, SendInput), WindowTheming (title bar gelap DWM),
                               WinEventHook (pembungkus hook, UI thread), InputSender (fokuskan scrcpy + SendInput kombinasi tombol), WindowGeometry (frame/klien/DPI/area kerja)
  Resources/Strings.resx       SEMUA teks UI (bahasa Indonesia). XAML: {loc:Str Kunci}; C#: Strings.Get("Kunci") / Strings.Format
tests/Mirraawwrring.Core.Tests/ xUnit; Fakes/ berisi FakeProcessRunner, FakeFileSystem, FakeAppPaths, FakeAdbClient, FakeTimeProvider
config/scrcpy-manifest.json    versi scrcpy yang di-pin + URL + SHA-256 (disalin ke samping exe saat build)
reference/                     prototipe PowerShell, mockup desain (*.dc.html), scrcpy-4.1-help.txt (output --help resmi)
docs/manual-test.md            checklist uji dengan HP sungguhan
```

## Konvensi

- **Core tidak tahu UI.** Semua proses eksternal lewat `IProcessRunner`; di test diganti `FakeProcessRunner`. Tidak ada `Process.Start` di luar `ProcessRunner`.
- **Parser = fungsi statis murni** (`AdbDevicesParser.Parse`, `ScrcpyLogClassifier.Classify`, …) dengan unit test dari output nyata (`PROMPT.md` §11).
- **Satu tempat per pengetahuan:** argumen scrcpy hanya di `ScrcpyArgumentBuilder`; lokasi tools hanya di `ToolLocator`; path hanya di `AppPaths`; perintah toolbar nanti hanya di `DeviceActions`; P/Invoke hanya di `Interop/NativeMethods.cs`.
- **Async + timeout:** `ProcessRequest.DefaultTimeout` = 10 s; pair/connect 20 s (`AdbClient.NetworkTimeout`). Jangan blokir UI thread; event dari Core (DeviceWatcher, LogBuffer) datang dari thread pool, marshal ke Dispatcher di ViewModel.
- **Logging:** `ILogger<T>` dari DI. Setiap perintah eksternal otomatis tercatat oleh `ProcessRunner` ("> adb devices -l" + output). Kategori dipendekkan ke nama kelas. File: `data\logs\Mirraawwrring-yyyyMMdd.log`.
- **adb shell:** selalu lewat `IAdbClient.ShellAsync(serial, ["cmd", "arg"])`; escaping POSIX dilakukan `ShellQuoting`.
- **Teks UI:** tambah kunci di `Strings.resx` (konvensi `<Layar>_<Elemen>`), jangan hardcode string di XAML/VM.
- **Tema:** warna lewat `{StaticResource Brush.*}`; style baru ditambahkan di `Controls.xaml` dengan nama `Style.<Kontrol>[.<Varian>]`. Placeholder TextBox: `Tag="teks"`.
- **Dialog:** Window transient di DI + `Func<Window>` factory untuk MainWindow; VM dialog punya event `Completed`, code-behind set `DialogResult = true`. Tombol dengan konten non-teks wajib `AutomationProperties.Name` (aksesibilitas, dan dipakai uji UI otomatis).
- **Uji UI otomatis** (tanpa HP): skrip PowerShell UIAutomation (`-STA`) bisa meng-invoke tombol berdasarkan `AutomationProperties.Name`, mengisi TextBox lewat ValuePattern, dan screenshot jendela dengan `PrintWindow`. Contoh skrip pernah dipakai di sesi 14 Okt 2026 (scratchpad); buat ulang kalau perlu.
  Untuk alur yang butuh HP (toolbar, nanti file browser), Fase 5 memakai **adb & scrcpy palsu**: dua program .NET kecil bernama `adb.exe` (menjawab `devices -l`, `shell getprop/input/settings/cmd/wm`, `exec-out screencap -p` → PNG dummy, `install`; file `BLOCK` di foldernya = INJECT_EVENTS) dan `scrcpy.exe` (jendela WPF berjudul `--window-title`, mencetak `KEY: Ctrl+V` saat menerima tombol; `--no-window` menulis byte dummy ke `--record`). Folder berisi keduanya + `scrcpy-server` dummy dipakai lewat `customScrcpyPath` di `data\settings.json` setelah folder `scrcpy\` di bin disembunyikan. Jendela toolbar (`WS_EX_TOOLWINDOW`) tidak muncul di daftar anak root UIA; pakai `EnumWindows` untuk mencarinya. Untuk file browser, adb palsu memetakan `/sdcard/...` ke folder lokal `fakefs/` (find/pull/push/mkdir/mv/rm) sehingga seluruh alur panel bisa diuji; MessageBox konfirmasi paling mudah dijawab lewat SendKeys ("y"/"n") karena tombolnya tidak terlihat UIA dari jendela NOACTIVATE.
- **Layered window (toolbar):** piksel yang alpha-nya 0 tembus klik ke jendela di bawahnya. Elemen yang harus bisa diklik tetapi "transparan" (pegangan resize) diberi `Background="#01000000"`. Kolom samping toolbar = panel file (bila terbuka, lebar `FilesPanelWidth`) atau 196 px untuk toast/legenda; `ToolbarWindow.WindowWidthDip` menghitungnya.
- **Toolbar:** jangan `Activate()`/fokus ke toolbar; `WS_EX_NOACTIVATE` disengaja supaya scrcpy tetap menerima keyboard. Posisi selalu dihitung dalam piksel fisik dengan DPI jendela scrcpy (`GetDpiForWindow`), lalu `SetWindowPos`; ukuran di-set dalam DIP. Aksi yang butuh injeksi lewat `RunInjectionAsync` (buka panduan bila diblokir); output yang memuat `INJECT_EVENTS` otomatis menandai blokir.
- **Fase:** di akhir fase: build bersih, test lulus, CHANGELOG, commit conventional (`feat:`, `fix:`, `docs:`, …), lalu tunggu konfirmasi pemilik.

## Alur data utama

```
ToolLocator → ToolRegistry ──► AdbClient ──► DeviceWatcher ──► MainViewModel.Devices (DeviceItemViewModel)
                        └────► ScrcpySessionManager.Start(ScrcpyOptions, Device) ──► ScrcpySession (events: LogLine, InjectionBlockedDetected, Exited)
ScrcpyArgumentBuilder(ScrcpyOptions) ──► ScrcpyLaunchPlan (argumen + judul jendela + path rekaman)
ProcessRunner ──► ILogger ──► LogBuffer (panel Log) + FileLogWriter
MainViewModel ⇄ ISettingsStore (Options, LastDeviceSerial)
```

Event Core datang dari thread pool; `MainViewModel` memakai `SynchronizationContext` yang ditangkap di konstruktor (`Post`) untuk kembali ke UI thread. Perintah `adb devices` polling memakai `ProcessRequest.Quiet` (log Debug).

## Resep

### Menambah tombol baru di toolbar (Fase 5+)
1. `Core/Devices/DeviceActions.cs`: tambah metode (perintah adb lewat `IAdbClient.ShellAsync`), tandai apakah butuh injeksi input.
2. ViewModel toolbar: tambah `[RelayCommand]` yang memanggil DeviceActions dan menandai `RequiresInjection`.
3. `Views/ToolbarWindow.xaml`: tambah `Button` (40×40) dengan ikon Path, `AutomationProperties.Name`, dan `ToolTip`.
4. `Strings.resx`: kunci tooltip/nama; shortcut scrcpy (jika ada) dari bagian "Shortcuts" di `reference/scrcpy-4.1-help.txt`.
5. Test: perintah yang dibentuk DeviceActions (lewat `FakeProcessRunner`).

### Menambah opsi scrcpy baru
1. `Core/Scrcpy/ScrcpyOptions.cs`: tambah properti.
2. `Core/Scrcpy/ScrcpyArgumentBuilder.cs`: petakan ke flag; cek nama flag di `reference/scrcpy-4.1-help.txt` (jangan menebak). Tulis aturan konflik di komentar kelas.
3. `MainViewModel`: tambah `[ObservableProperty]` + `partial void On<Nama>Changed → SaveOptions()`, masukkan ke `CurrentOptions` dan `LoadOptions`.
4. UI: checkbox/combobox di `MainWindow.xaml` (panel Opsi), teks di `Strings.resx`.
5. Test: `ScrcpyArgumentBuilderTests`; `SettingsStoreTests` kalau tipe datanya baru.

### Menambah merek HP ke tabel VID
1. `Core/Usb/UsbVendorTable.cs`: tambah baris `["XXXX"] = "Merek"` (4 digit heksadesimal huruf besar) di `Vendors`.
2. Tambah `InlineData` di `UsbProbeParserTests.VendorTable`.
VID dibaca dari `DeviceID` (`USB\VID_XXXX&PID_YYYY\…`) oleh `UsbProbeParser`; antarmuka ADB dikenali dari CompatibleID `USB\Class_FF&SubClass_42&Prot_01`.

### Menambah pemeriksaan baru di wizard Verifikasi
1. `Core/Setup/VerificationService.cs`: tambah anggota `CheckId`, metode `Check…Async`, panggil di `RunAsync` (urutan = urutan tampil), tambahkan ke `AllChecks`.
2. `ViewModels/Setup/VerifyPageViewModel.cs`: tambah `VerifyRowViewModel` di konstruktor (judul, judul gagal, deskripsi gagal) dengan `CheckGroup` yang sesuai.
3. Kalau butuh tombol perbaikan: tambah `FixAction` + penanganannya di `SetupViewModel.OnFixRequested`, teks di `Strings.resx` (`Verify_Fix…`), dan `VerifyRowViewModel.FixText`.
4. Test di `VerificationServiceTests`.

### Memperbarui versi scrcpy yang dibundel
1. Ambil SHA-256 dari `doc/windows.md` di tag rilis resmi scrcpy (bukan dari sumber lain).
2. Perbarui `config/scrcpy-manifest.json` (version, url, sha256, folderInZip).
3. Unduh paketnya, jalankan `scrcpy --help`, simpan ke `reference/scrcpy-<versi>-help.txt`, dan cek semua flag yang dipakai `ScrcpyArgumentBuilder` masih ada.
4. Jalankan test; perbarui CHANGELOG dan README.

## Keterbatasan yang diketahui

- **Xiaomi/HyperOS:** injeksi input diblokir sampai "USB debugging (Security settings)" aktif (butuh akun Xiaomi + data seluler). Terdeteksi dari log scrcpy (`ScrcpyLogKind.InjectionBlocked`) dan nanti `InjectionProbe`. Alternatif: Input UHID.
- **`--mouse=uhid`** menangkap mouse PC (mode relatif); LAlt/LSuper melepasnya. Jelaskan di tooltip opsi UHID.
- **Aplikasi tidak di-sign:** SmartScreen akan memperingatkan; README menjelaskan "Run anyway" dan Unblock.
- **adb lain di PATH** (Android SDK) bisa bentrok di port 5037; aplikasi selalu memakai adb dari folder scrcpy dan mengatur env `ADB` saat menjalankan scrcpy.
- **Menutup aplikasi menghentikan semua sesi scrcpy** (ScrcpySessionManager.Dispose lewat DI). Toolbar bergantung pada aplikasi, jadi ini disengaja.
- **Rekaman dari toolbar = .mkv**, bukan .mp4: proses scrcpy `--no-window` hanya bisa dihentikan dengan kill, dan MP4 tanpa trailer tidak bisa diputar. Rekaman dari opsi "Rekam ke file" di jendela utama tetap .mp4 (scrcpy ditutup normal lewat jendelanya).
- **Cadangan `cmd media_session volume --adj`** saat injeksi diblokir belum diverifikasi di HyperOS. Cadangan `screenrecord` untuk rekaman tidak dibuat (rekaman tidak butuh injeksi).
- **Hotkey screenshot** memakai `RegisterHotKey` global yang hanya didaftarkan saat jendela scrcpy/toolbar foreground; kalau aplikasi lain sudah memakai kombinasi itu, pendaftaran gagal dan dicatat di log. Ubah lewat `screenshotHotkey` di settings.json (belum ada UI).
- **DPI campuran** (monitor dengan skala berbeda) belum diuji dengan sungguhan; PC pengembang 100%.
- **Format `stat` toybox** (`%F|%s|%Y|%n`) belum diverifikasi di HP sungguhan; parser toleran (baris tak cocok dilewati dan dicatat Debug). Progres di dalam satu file besar tidak diketahui (adb pull/push mencetak progres dengan `\r`, bukan baris).
- **Hapus di HP** selalu lewat dialog konfirmasi (`FilesPanelViewModel.Confirm`); jangan tambahkan jalur hapus tanpa konfirmasi (PROMPT.md §9).
- Pengembangan tanpa scrcpy terpasang: salin isi `scrcpy-win64-v4.1` ke `src/Mirraawwrring.App/bin/Debug/net10.0-windows/scrcpy/` (folder bin tidak masuk git).
- Fitur yang butuh HP fisik belum teruji otomatis; lihat `docs/manual-test.md`.
