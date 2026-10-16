using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Input;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Scrcpy;
using Mirraawwrring.Core.Settings;

namespace Mirraawwrring.App.ViewModels;

/// <summary>Isi tooltip tombol toolbar: nama + pintasan (badge). Saat injeksi diblokir, teksnya diganti penjelasan.</summary>
public sealed record ToolbarTip(string Text, string? Shortcut);

/// <summary>Satu notifikasi kecil di samping toolbar (§6.7).</summary>
public sealed record ToastItem(string Title, string Body, string? FilePath, string? FolderPath, bool IsError)
{
    public bool HasOpen => FilePath is not null;
    public bool HasFolder => FolderPath is not null;
}

/// <summary>
/// ViewModel toolbar samping untuk satu sesi scrcpy (§5.8, §6.5). Semua perintah ke HP lewat <see cref="DeviceActions"/>;
/// tombol yang butuh injeksi dicek terhadap <see cref="InjectionProbe"/> dan dialihkan ke panduan HyperOS jika diblokir.
/// </summary>
public sealed partial class ToolbarViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(7);
    public static readonly TimeSpan LegendDuration = TimeSpan.FromSeconds(12);
    public const double MinFilesPanelWidth = 380;
    public const double MaxFilesPanelWidth = 1000;

    private readonly DeviceActions _actions;
    private readonly InjectionProbe _injection;
    private readonly ScrcpySessionManager _sessions;
    private readonly ISettingsStore _settings;
    private readonly IAppPaths _paths;
    private readonly ILogger<ToolbarViewModel> _logger;
    private readonly SynchronizationContext _ui;
    private ScrcpySession? _session;
    private ScrcpySession? _recording;
    private Device? _device;
    private ScrcpyOptions _options = new();
    private CancellationTokenSource? _toastCts;
    private CancellationTokenSource? _legendCts;
    private bool _stopRequested;

    public ToolbarViewModel(DeviceActions actions, InjectionProbe injection, ScrcpySessionManager sessions, ISettingsStore settings, IAppPaths paths, FilesPanelViewModel filesPanel, ILogger<ToolbarViewModel> logger)
    {
        FilesPanel = filesPanel;
        FilesPanelWidth = Math.Clamp(settings.Current.FilesPanelWidth, MinFilesPanelWidth, MaxFilesPanelWidth);
        filesPanel.ToastRequested += ShowToast;
        filesPanel.CloseRequested += () => IsFilesOpen = false;
        _actions = actions;
        _injection = injection;
        _sessions = sessions;
        _settings = settings;
        _paths = paths;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        ScreenshotHotkey = Hotkey.ParseOrDefault(settings.Current.ScreenshotHotkey);
        UpdateTips();
    }

    public string Serial => _device?.Serial ?? string.Empty;

    public string DisplayName => _session?.DisplayName ?? string.Empty;

    /// <summary>HWND jendela scrcpy; diisi View setelah jendelanya muncul (untuk SendInput).</summary>
    public nint ScrcpyWindowHandle { get; set; }

    public Hotkey ScreenshotHotkey { get; }

    public event Action? GuideRequested;

    /// <summary>View menampilkan dialog pilih file APK dan mengembalikan path-nya (null = batal).</summary>
    public Func<string?>? PickApkFile { get; set; }

    public void Initialize(ScrcpySession session, Device device, ScrcpyOptions options)
    {
        _session = session;
        _device = device;
        _options = options;
        OnPropertyChanged(nameof(Serial));
        OnPropertyChanged(nameof(DisplayName));
        FilesPanel.Initialize(device.Serial, session.DisplayName);
        session.InjectionBlockedDetected += OnInjectionBlockedBySession;
        if (session.InjectionBlocked)
        {
            SetInjectionBlocked(true);
        }

        // Uji ulang tiap sesi: Security settings bisa berubah di antara dua sesi, jadi cache InjectionProbe tidak dipakai di sini.
        _ = ProbeInjectionAsync();

        _ = RefreshShowTouchesAsync();
    }

    // ---------- Status ----------

    [ObservableProperty]
    public partial bool InjectionBlocked { get; set; }

    [ObservableProperty]
    public partial bool LegendVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordTip))]
    public partial bool IsRecording { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotificationsTip))]
    public partial bool NotificationsExpanded { get; set; }

    [ObservableProperty]
    public partial bool ShowTouches { get; set; }

    [ObservableProperty]
    public partial ToastItem? Toast { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    // ---------- Tooltip ----------

    [ObservableProperty] public partial ToolbarTip PowerTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip VolumeUpTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip VolumeDownTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip BackTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip HomeTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip RecentsTip { get; set; } = new(string.Empty, null);
    [ObservableProperty] public partial ToolbarTip SendClipboardTip { get; set; } = new(string.Empty, null);

    public ToolbarTip RotateTip { get; } = new(Strings.Get("Toolbar_Rotate"), Strings.Get("Toolbar_KeyRotate"));
    public ToolbarTip NotificationsTip => new(Strings.Get(NotificationsExpanded ? "Toolbar_NotificationsClose" : "Toolbar_Notifications"), Strings.Get("Toolbar_KeyNotifications"));
    public ToolbarTip ScreenshotTip => new(Strings.Get("Toolbar_Screenshot"), ScreenshotHotkey.Display);
    public ToolbarTip RecordTip => new(Strings.Get(IsRecording ? "Toolbar_RecordStop" : "Toolbar_Record"), null);
    public ToolbarTip FilesTip { get; } = new(Strings.Get("Toolbar_Files"), null);

    // ---------- File browser (Fase 6) ----------

    public FilesPanelViewModel FilesPanel { get; }

    [ObservableProperty]
    public partial bool IsFilesOpen { get; set; }

    /// <summary>Lebar panel file (DIP), diubah lewat pegangan resize di View; disimpan ke settings saat selesai digeser.</summary>
    [ObservableProperty]
    public partial double FilesPanelWidth { get; set; }

    [RelayCommand]
    private async Task ToggleFilesAsync()
    {
        IsFilesOpen = !IsFilesOpen;
        if (IsFilesOpen)
        {
            await FilesPanel.EnsureLoadedAsync();
        }
    }

    public void SaveFilesPanelWidth()
    {
        var width = FilesPanelWidth;
        Task.Run(() => _settings.Update(s => s.FilesPanelWidth = width));
    }

    private void ShowToast(ToastItem item) => ShowToast(item.Title, item.Body, item.IsError, item.FilePath, item.FolderPath);
    public ToolbarTip InstallApkTip { get; } = new(Strings.Get("Toolbar_InstallApk"), null);
    public ToolbarTip MoreTip { get; } = new(Strings.Get("Toolbar_More"), null);

    private void UpdateTips()
    {
        var blocked = InjectionBlocked;
        PowerTip = Tip(blocked, "Toolbar_Power", "Toolbar_KeyPower");
        VolumeUpTip = Tip(blocked, "Toolbar_VolumeUp", "Toolbar_KeyVolumeUp");
        VolumeDownTip = Tip(blocked, "Toolbar_VolumeDown", "Toolbar_KeyVolumeDown");
        BackTip = Tip(blocked, "Toolbar_Back", "Toolbar_KeyBack");
        HomeTip = Tip(blocked, "Toolbar_Home", "Toolbar_KeyHome");
        RecentsTip = Tip(blocked, "Toolbar_Recents", "Toolbar_KeyRecents");
        SendClipboardTip = Tip(blocked, "Toolbar_SendClipboard", "Toolbar_KeySendClipboard");
    }

    private static ToolbarTip Tip(bool blocked, string nameKey, string keyKey) =>
        blocked ? new ToolbarTip(Strings.Get("Toolbar_Blocked"), null) : new ToolbarTip(Strings.Get(nameKey), Strings.Get(keyKey));

    // ---------- Injeksi ----------

    private async Task ProbeInjectionAsync()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            var status = await _injection.RecheckAsync(_device.Serial);
            Post(() =>
            {
                if (status == InjectionStatus.Blocked)
                {
                    SetInjectionBlocked(true);
                }
                else if (status == InjectionStatus.Allowed && !(_session?.InjectionBlocked ?? false))
                {
                    SetInjectionBlocked(false);
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Uji injeksi gagal");
        }
    }

    private void OnInjectionBlockedBySession(ScrcpySession _) => Post(() => SetInjectionBlocked(true));

    private void SetInjectionBlocked(bool blocked)
    {
        if (InjectionBlocked == blocked)
        {
            return;
        }

        InjectionBlocked = blocked;
        UpdateTips();
        if (blocked)
        {
            ShowLegend();
        }
        else
        {
            LegendVisible = false;
        }
    }

    private void ShowLegend()
    {
        _legendCts?.Cancel();
        var cts = _legendCts = new CancellationTokenSource();
        LegendVisible = true;
        _ = HideLaterAsync(LegendDuration, cts.Token, () => LegendVisible = false);
    }

    [RelayCommand]
    private void ShowGuide()
    {
        LegendVisible = false;
        GuideRequested?.Invoke();
    }

    /// <summary>Jalankan aksi yang butuh injeksi; kalau diblokir, buka panduan (§6.5).</summary>
    private async Task RunInjectionAsync(DeviceAction action, Func<string, Task<ProcessResult>> run)
    {
        if (InjectionBlocked)
        {
            ShowGuide();
            return;
        }

        await RunAsync(action, run);
    }

    private async Task RunAsync(DeviceAction action, Func<string, Task<ProcessResult>> run)
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            var result = await run(_device.Serial);
            if (!result.Succeeded)
            {
                var output = result.CombinedOutput;
                if (output.Contains(InjectionProbe.BlockedMarker, StringComparison.Ordinal))
                {
                    _injection.Forget(_device.Serial);
                    SetInjectionBlocked(true);
                    ShowGuide();
                    return;
                }

                ShowToast(Strings.Get("Toast_ActionFailed"), $"{action}: {FirstLine(output)}", error: true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Aksi {Action} gagal", action);
            ShowToast(Strings.Get("Toast_ActionFailed"), ex.Message, error: true);
        }
    }

    // ---------- Tombol ----------

    /// <summary>Power: HP yang tidur dibangunkan (KEYCODE_WAKEUP, lihat <see cref="DeviceActions.PowerAsync"/>) dan diberi toast petunjuk; HP yang bangun ditidurkan.</summary>
    [RelayCommand]
    private async Task PowerAsync()
    {
        PowerOutcome? outcome = null;
        await RunInjectionAsync(DeviceAction.Power, async s => (outcome = await _actions.PowerAsync(s)).Result);
        if (outcome is { WokeUp: true, Result.Succeeded: true })
        {
            ShowToast(Strings.Get("Toast_WokeUp"), Strings.Get("Toast_WokeUpHint"), error: false);
        }
    }

    [RelayCommand]
    private Task VolumeUpAsync() => VolumeAsync(up: true);

    [RelayCommand]
    private Task VolumeDownAsync() => VolumeAsync(up: false);

    /// <summary>Volume: saat injeksi diblokir pakai cadangan <c>cmd media_session volume</c>; kalau itu pun gagal, buka panduan.</summary>
    private async Task VolumeAsync(bool up)
    {
        if (_device is null)
        {
            return;
        }

        if (!InjectionBlocked)
        {
            await RunAsync(up ? DeviceAction.VolumeUp : DeviceAction.VolumeDown, s => _actions.VolumeAsync(s, up, useFallback: false));
            return;
        }

        try
        {
            var result = await _actions.VolumeAsync(_device.Serial, up, useFallback: true);
            if (!result.Succeeded)
            {
                ShowGuide();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Volume cadangan gagal");
            ShowGuide();
        }
    }

    [RelayCommand]
    private Task RotateAsync() => RunAsync(DeviceAction.Rotate, s => _actions.RotateAsync(s));

    [RelayCommand]
    private Task BackAsync() => RunInjectionAsync(DeviceAction.Back, s => _actions.BackAsync(s));

    [RelayCommand]
    private Task HomeAsync() => RunInjectionAsync(DeviceAction.Home, s => _actions.HomeAsync(s));

    [RelayCommand]
    private Task RecentsAsync() => RunInjectionAsync(DeviceAction.Recents, s => _actions.RecentsAsync(s));

    [RelayCommand]
    private async Task NotificationsAsync()
    {
        var expand = !NotificationsExpanded;
        await RunAsync(DeviceAction.Notifications, s => _actions.SetNotificationsAsync(s, expand));
        NotificationsExpanded = expand;
    }

    [RelayCommand]
    private async Task ScreenshotAsync()
    {
        if (_device is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _actions.ScreenshotAsync(_device.Serial);
            if (!result.Success)
            {
                ShowToast(Strings.Get("Toast_ScreenshotFailed"), result.Message, error: true);
                return;
            }

            var copied = CopyPngToClipboard(result.Png!);
            var folder = Path.GetDirectoryName(result.FilePath)!;
            var body = copied ? Strings.Format("Toast_ScreenshotBody", ShortFolder(folder)) : ShortFolder(folder);
            ShowToast(Strings.Get("Toast_ScreenshotSaved"), body, error: false, result.FilePath, folder);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Screenshot gagal");
            ShowToast(Strings.Get("Toast_ScreenshotFailed"), ex.Message, error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CopyPngToClipboard(byte[] png)
    {
        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            using var stream = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Clipboard.SetImage(image);
            return true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or NotSupportedException or IOException)
        {
            _logger.LogWarning(ex, "Screenshot tidak bisa disalin ke clipboard");
            return false;
        }
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_device is null)
        {
            return;
        }

        if (_recording is { IsRunning: true } active)
        {
            _stopRequested = true;
            active.Stop();
            return;
        }

        try
        {
            _stopRequested = false;
            var session = _sessions.StartHeadlessRecording(_device, withAudio: _options.ForwardAudio);
            _recording = session;
            session.Exited += OnRecordingExited;
            IsRecording = true;
            _logger.LogInformation("{Command}", session.Plan.ToCommandLine());
            ShowToast(Strings.Get("Toast_RecordingStarted"), Strings.Get("Toast_RecordingBody"), error: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Core.Tools.ToolNotFoundException)
        {
            _logger.LogError(ex, "Rekam layar gagal dimulai");
            ShowToast(Strings.Get("Toast_RecordingFailed"), ex.Message, error: true);
        }
    }

    private void OnRecordingExited(ScrcpySession session, int code) => Post(() =>
    {
        session.Exited -= OnRecordingExited;
        if (!ReferenceEquals(session, _recording))
        {
            return;
        }

        _recording = null;
        IsRecording = false;
        var file = session.Plan.RecordFilePath;
        var exists = file is not null && File.Exists(file) && new FileInfo(file).Length > 0;
        if (exists && (_stopRequested || code == 0))
        {
            ShowToast(Strings.Get("Toast_RecordingSaved"), ShortFolder(Path.GetDirectoryName(file)!), error: false, file, Path.GetDirectoryName(file));
        }
        else
        {
            ShowToast(Strings.Get("Toast_RecordingFailed"), $"scrcpy exit {code}", error: true, null, exists ? Path.GetDirectoryName(file) : null);
        }
    });

    [RelayCommand]
    private async Task InstallApkAsync()
    {
        if (_device is null || PickApkFile is null || IsBusy)
        {
            return;
        }

        var path = PickApkFile();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        IsBusy = true;
        ShowToast(Strings.Get("Toast_ApkInstalling"), Path.GetFileName(path), error: false, sticky: true);
        try
        {
            var result = await _actions.InstallApkAsync(_device.Serial, path);
            if (result.Succeeded && !result.CombinedOutput.Contains("Failure", StringComparison.Ordinal))
            {
                ShowToast(Strings.Get("Toast_ApkInstalled"), Path.GetFileName(path), error: false);
            }
            else
            {
                ShowToast(Strings.Get("Toast_ApkFailed"), FirstLine(result.CombinedOutput.Length > 0 ? result.CombinedOutput : $"exit {result.ExitCode}"), error: true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Install APK gagal");
            ShowToast(Strings.Get("Toast_ApkFailed"), ex.Message, error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendClipboardAsync()
    {
        if (InjectionBlocked)
        {
            ShowGuide();
            return;
        }

        bool hasText;
        try
        {
            hasText = Clipboard.ContainsText();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            hasText = false;
        }

        if (!hasText)
        {
            ShowToast(Strings.Get("Toast_ClipboardEmpty"), string.Empty, error: true);
            return;
        }

        var ok = await InputSender.SendChordAsync(ScrcpyWindowHandle, DeviceActions.Chords.Paste);
        if (ok)
        {
            ShowToast(Strings.Get("Toast_ClipboardSent"), DeviceActions.Chords.Paste.Display, error: false);
        }
        else
        {
            ShowToast(Strings.Get("Toast_ClipboardNoWindow"), string.Empty, error: true);
        }
    }

    // ---------- Menu "Lainnya" ----------

    [RelayCommand]
    private async Task ToggleShowTouchesAsync()
    {
        var on = !ShowTouches;
        await RunAsync(DeviceAction.More, s => _actions.SetShowTouchesAsync(s, on));
        ShowTouches = on;
    }

    private async Task RefreshShowTouchesAsync()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            var on = await _actions.GetShowTouchesAsync(_device.Serial);
            Post(() => ShowTouches = on);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "show_touches tidak terbaca");
        }
    }

    [RelayCommand]
    private Task ScreenOffAsync() => SendChordAsync(DeviceActions.Chords.ScreenOff, "Toast_ScreenOff");

    /// <summary>
    /// MOD+Shift+o hanya menyalakan panel layar; HP yang sudah tidur/terkunci tidak bangun karenanya. Jadi bila injeksi tersedia,
    /// kirim KEYCODE_WAKEUP dulu (gagal diam-diam, kecuali ketahuan diblokir), lalu kombinasi tombolnya ke jendela scrcpy.
    /// </summary>
    [RelayCommand]
    private async Task ScreenOnAsync()
    {
        if (!InjectionBlocked && _device is not null)
        {
            try
            {
                var result = await _actions.WakeUpAsync(_device.Serial);
                if (!result.Succeeded && result.CombinedOutput.Contains(InjectionProbe.BlockedMarker, StringComparison.Ordinal))
                {
                    _injection.Forget(_device.Serial);
                    SetInjectionBlocked(true);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "KEYCODE_WAKEUP gagal");
            }
        }

        await SendChordAsync(DeviceActions.Chords.ScreenOn, "Toast_ScreenOn");
    }

    private async Task SendChordAsync(ScrcpyKeyChord chord, string toastKey)
    {
        var ok = await InputSender.SendChordAsync(ScrcpyWindowHandle, chord);
        if (ok)
        {
            ShowToast(Strings.Get(toastKey), chord.Display, error: false);
        }
        else
        {
            ShowToast(Strings.Get("Toast_ClipboardNoWindow"), chord.Display, error: true);
        }
    }

    [RelayCommand]
    private void OpenScreenshotsFolder() => OpenFolder(_paths.ScreenshotsDirectory);

    [RelayCommand]
    private void OpenRecordingsFolder() => OpenFolder(_paths.RecordingsDirectory);

    [RelayCommand]
    private async Task CopyDeviceInfoAsync()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            var text = await _actions.GetDeviceInfoTextAsync(_device.Serial);
            Clipboard.SetText(text);
            ShowToast(Strings.Get("Toast_InfoCopied"), FirstLine(text), error: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Salin info device gagal");
            ShowToast(Strings.Get("Toast_ActionFailed"), ex.Message, error: true);
        }
    }

    // ---------- Toast ----------

    private void ShowToast(string title, string body, bool error, string? filePath = null, string? folderPath = null, bool sticky = false)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        Toast = new ToastItem(title, body, filePath, folderPath, error);
        if (!sticky)
        {
            _ = HideLaterAsync(ToastDuration, cts.Token, () => Toast = null);
        }
    }

    private static async Task HideLaterAsync(TimeSpan delay, CancellationToken ct, Action hide)
    {
        try
        {
            await Task.Delay(delay, ct);
            hide();
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void DismissToast()
    {
        _toastCts?.Cancel();
        Toast = null;
    }

    [RelayCommand]
    private void ToastOpen()
    {
        var file = Toast?.FilePath;
        DismissToast();
        if (file is not null)
        {
            StartShell(file);
        }
    }

    [RelayCommand]
    private void ToastFolder()
    {
        var folder = Toast?.FolderPath;
        var file = Toast?.FilePath;
        DismissToast();
        if (file is not null && File.Exists(file))
        {
            StartShell("explorer.exe", $"/select,\"{file}\"");
        }
        else if (folder is not null)
        {
            OpenFolder(folder);
        }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Folder tidak bisa dibuat: {Folder}", folder);
        }

        StartShell("explorer.exe", $"\"{folder}\"");
    }

    private void StartShell(string fileName, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, arguments ?? string.Empty) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            _logger.LogWarning(ex, "Tidak bisa membuka {File}", fileName);
        }
    }

    private static string ShortFolder(string folder)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return folder.StartsWith(profile, StringComparison.OrdinalIgnoreCase) ? folder[(profile.Length + 1)..] : folder;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n')[0].TrimEnd('\r');
        return line.Length > 90 ? line[..90] + "…" : line;
    }

    private void Post(Action action) => _ui.Post(_ => action(), null);

    public void Dispose()
    {
        if (_session is not null)
        {
            _session.InjectionBlockedDetected -= OnInjectionBlockedBySession;
        }

        if (_recording is not null)
        {
            _recording.Exited -= OnRecordingExited;
            // Rekaman ikut berhenti saat sesi mirroring berakhir; file .mkv tetap bisa diputar.
            _recording.Stop();
        }

        _toastCts?.Cancel();
        _legendCts?.Cancel();
        FilesPanel.Dispose();
    }
}
