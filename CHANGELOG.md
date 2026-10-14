# Changelog

Semua perubahan penting dicatat di sini. Format mengikuti [Keep a Changelog](https://keepachangelog.com/id/1.1.0/), versi mengikuti SemVer.

## [Unreleased]

### Fase 0 — Fondasi
- Solution .NET 10 / WPF dengan tiga proyek: `Mirraawwrring.Core`, `Mirraawwrring.App`, `Mirraawwrring.Core.Tests`.
- `Directory.Build.props` (versi aplikasi, LangVersion, Nullable, x64, TreatWarningsAsErrors untuk Core).
- App manifest Per-Monitor DPI Aware v2, `asInvoker`.
- DI dengan `Microsoft.Extensions.DependencyInjection`, MVVM dengan `CommunityToolkit.Mvvm`.
- Logging ke panel (`LogBuffer`) dan file harian di `data\logs` (simpan 7 hari) lewat `AppLoggerProvider`.
- `AppPaths`: mode portable (`data\` di samping exe) dengan fallback `%LOCALAPPDATA%\Mirraawwrring`.
- Tema gelap sendiri (`Themes/Colors.xaml`, `Themes/Controls.xaml`) sesuai token warna mockup; title bar native mode gelap lewat `DwmSetWindowAttribute`.
- `Strings.resx` + markup extension `{loc:Str Kunci}` untuk semua teks UI.
- `config/scrcpy-manifest.json` (scrcpy 4.1, SHA-256 terverifikasi terhadap unduhan resmi).
