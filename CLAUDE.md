# CLAUDE.md — peta proyek Mirraawwrring

Aplikasi desktop Windows (WPF, .NET 10, C#) untuk mirror dan kontrol HP Android lewat **scrcpy**. Spesifikasi lengkap ada di `PROMPT.md`; file ini adalah ringkasan operasional supaya sesi berikutnya bisa langsung bekerja. **Perbarui file ini setiap kali arsitektur berubah.**

Status fase (lihat `PROMPT.md` §10 dan `CHANGELOG.md`): Fase 0, 1, dan 2 selesai. Berikutnya: Fase 3 (koneksi nirkabel: dialog Pair wireless, Sambung via IP + riwayat, Aktifkan Wi-Fi dari USB, daftar HP via mDNS). Tombol-tombolnya sudah ada di `MainViewModel` (`PairWirelessCommand`, `ConnectViaIpCommand`, `EnableWifiFromUsbCommand`) dan saat ini hanya menampilkan pesan "fase berikutnya".

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
  Interop/                     NativeMethods (EnumWindows dsb.), WindowFinder (cari HWND jendela scrcpy dari PID)
src/Mirraawwrring.App/         WPF
  App.xaml(.cs)                DI container, logging, handler exception global, LocateTools() saat startup (pakai CustomScrcpyPath dari settings)
  Themes/Colors.xaml           token warna/font/radius dari mockup (§7). Jangan tulis hex di View.
  Themes/Icons.xaml            Geometry ikon garis (Icon.*) + Style.Icon
  Themes/Controls.xaml         style: Style.Window, Style.Button(.Accent/.Icon/.Subtle), Style.TextBox, Style.CheckBox, Style.Panel, Style.Card, ToolTip, ScrollBar
  Themes/Controls.Main.xaml    Style.ComboBox, Style.ModeCard (RadioButton kartu), Style.DeviceList/DeviceItem, Style.Badge
  Converters/Converters.cs     EnumToBool (RadioButton↔enum), InvertedBoolToVisibility, NullToCollapsed
  Views/MainWindow             jendela utama (mockup Main/Empty); HyperOsGuideWindow = panduan Security settings
  ViewModels/MainViewModel     daftar perangkat, mode/kualitas/opsi (disimpan ke settings), log, status, Mulai/Hentikan mirroring
  ViewModels/DeviceItemViewModel  satu kartu perangkat (subtitle, badge, IsMirroring)
  Interop/                     NativeMethods (P/Invoke UI), WindowTheming (title bar gelap DWM)
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
- **Tema:** warna lewat `{StaticResource Brush.*}`; style baru ditambahkan di `Controls.xaml` dengan nama `Style.<Kontrol>[.<Varian>]`.
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

### Menambah merek HP ke tabel VID (Fase 4)
1. `Core/Usb/UsbVendorTable.cs`: tambah baris `VID → merek`.
2. Test tabel.

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
- **Menutup aplikasi menghentikan semua sesi scrcpy** (ScrcpySessionManager.Dispose lewat DI). Toolbar (Fase 5) bergantung pada aplikasi, jadi ini disengaja.
- Pengembangan tanpa scrcpy terpasang: salin isi `scrcpy-win64-v4.1` ke `src/Mirraawwrring.App/bin/Debug/net10.0-windows/scrcpy/` (folder bin tidak masuk git).
- Fitur yang butuh HP fisik belum teruji otomatis; lihat `docs/manual-test.md`.
