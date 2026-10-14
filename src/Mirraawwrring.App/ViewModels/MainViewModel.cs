using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Logging;
using Mirraawwrring.Core.Scrcpy;
using Mirraawwrring.Core.Settings;
using Mirraawwrring.Core.Tools;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.App.ViewModels;

/// <summary>Pilihan di ComboBox kualitas.</summary>
public sealed record Choice<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public sealed record LogLineItem(string Text, bool IsProblem);

/// <summary>ViewModel jendela utama (mockup Main/Empty). Semua event Core di-marshal ke UI thread lewat SynchronizationContext.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxLogLines = 500;

    private readonly DeviceWatcher _watcher;
    private readonly IAdbClient _adb;
    private readonly ScrcpySessionManager _sessions;
    private readonly ISettingsStore _settings;
    private readonly IToolRegistry _tools;
    private readonly LogBuffer _logBuffer;
    private readonly IAppPaths _paths;
    private readonly WirelessService _wireless;
    private readonly MdnsWatcher _mdns;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SynchronizationContext _ui;
    private readonly Dictionary<string, DeviceDetails> _detailsCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _detailsInFlight = new(StringComparer.Ordinal);
    private bool _loadingOptions;
    private bool _adbServerOk;
    private bool _initialized;

    public MainViewModel(
        DeviceWatcher watcher,
        IAdbClient adb,
        ScrcpySessionManager sessions,
        ISettingsStore settings,
        IToolRegistry tools,
        LogBuffer logBuffer,
        IAppPaths paths,
        WirelessService wireless,
        MdnsWatcher mdns,
        ILogger<MainViewModel> logger)
    {
        _wireless = wireless;
        _mdns = mdns;
        _watcher = watcher;
        _adb = adb;
        _sessions = sessions;
        _settings = settings;
        _tools = tools;
        _logBuffer = logBuffer;
        _paths = paths;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        ResolutionChoices =
        [
            new Choice<int?>(Strings.Get("Resolution_Native"), null),
            new Choice<int?>(Strings.Format("Resolution_Px", 1920), 1920),
            new Choice<int?>(Strings.Format("Resolution_Px", 1280), 1280),
            new Choice<int?>(Strings.Format("Resolution_Px", 1024), 1024),
        ];
        BitrateChoices =
        [
            new Choice<int>(Strings.Format("Bitrate_Mbps", 8), 8),
            new Choice<int>(Strings.Format("Bitrate_Mbps", 4), 4),
            new Choice<int>(Strings.Format("Bitrate_Mbps", 2), 2),
        ];
        FpsChoices =
        [
            new Choice<int?>("60", 60),
            new Choice<int?>("30", 30),
            new Choice<int?>(Strings.Get("Fps_Unlimited"), null),
        ];

        LoadOptions(_settings.Current.Options);

        foreach (var entry in _logBuffer.Snapshot())
        {
            AppendLog(entry);
        }

        _logBuffer.EntryAdded += OnLogEntry;
        _watcher.DevicesChanged += OnDevicesChanged;
        _sessions.SessionStarted += OnSessionChanged;
        _sessions.SessionEnded += OnSessionChanged;
        _tools.Changed += _ => Post(UpdateStatus);
        _mdns.ServicesChanged += OnMdnsChanged;

        DeviceCountText = Strings.Get("Main_DeviceCountSearching");
        UpdateStatus();
    }

    // ---------- Perangkat ----------

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceSelected))]
    [NotifyPropertyChangedFor(nameof(IsMirroringSelected))]
    [NotifyPropertyChangedFor(nameof(StartButtonText))]
    [NotifyCanExecuteChangedFor(nameof(StartMirroringCommand))]
    public partial DeviceItemViewModel? SelectedDevice { get; set; }

    [ObservableProperty]
    public partial string DeviceCountText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoDevices))]
    public partial bool HasDevices { get; set; }

    public bool HasNoDevices => !HasDevices;

    /// <summary>Panel mode aktif hanya jika perangkat siap dipilih.</summary>
    public bool IsDeviceSelected => SelectedDevice is { IsReady: true };

    public bool IsMirroringSelected => SelectedDevice is { } d && _sessions.Find(d.Serial) is not null;

    public string StartButtonText => IsMirroringSelected ? Strings.Get("Main_StopMirroring") : Strings.Get("Main_StartMirroring");

    // ---------- Mode / kualitas / opsi ----------

    [ObservableProperty]
    public partial MirrorMode Mode { get; set; }

    public IReadOnlyList<Choice<int?>> ResolutionChoices { get; }

    [ObservableProperty]
    public partial Choice<int?> SelectedResolution { get; set; }

    public IReadOnlyList<Choice<int>> BitrateChoices { get; }

    [ObservableProperty]
    public partial Choice<int> SelectedBitrate { get; set; }

    public IReadOnlyList<Choice<int?>> FpsChoices { get; }

    [ObservableProperty]
    public partial Choice<int?> SelectedFps { get; set; }

    [ObservableProperty]
    public partial bool ForwardAudio { get; set; }

    [ObservableProperty]
    public partial bool UhidInput { get; set; }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial bool Fullscreen { get; set; }

    [ObservableProperty]
    public partial bool StayAwake { get; set; }

    [ObservableProperty]
    public partial bool RecordToFile { get; set; }

    partial void OnModeChanged(MirrorMode value) => SaveOptions();
    partial void OnSelectedResolutionChanged(Choice<int?> value) => SaveOptions();
    partial void OnSelectedBitrateChanged(Choice<int> value) => SaveOptions();
    partial void OnSelectedFpsChanged(Choice<int?> value) => SaveOptions();
    partial void OnForwardAudioChanged(bool value) => SaveOptions();
    partial void OnUhidInputChanged(bool value) => SaveOptions();
    partial void OnAlwaysOnTopChanged(bool value) => SaveOptions();
    partial void OnFullscreenChanged(bool value) => SaveOptions();
    partial void OnStayAwakeChanged(bool value) => SaveOptions();
    partial void OnRecordToFileChanged(bool value) => SaveOptions();

    public ScrcpyOptions CurrentOptions => new()
    {
        Mode = Mode,
        MaxSize = SelectedResolution.Value,
        VideoBitRateMbps = SelectedBitrate.Value,
        MaxFps = SelectedFps.Value,
        ForwardAudio = ForwardAudio,
        UhidInput = UhidInput,
        AlwaysOnTop = AlwaysOnTop,
        Fullscreen = Fullscreen,
        StayAwake = StayAwake,
        RecordToFile = RecordToFile,
    };

    private void LoadOptions(ScrcpyOptions o)
    {
        _loadingOptions = true;
        try
        {
            Mode = o.Mode;
            SelectedResolution = ResolutionChoices.FirstOrDefault(c => c.Value == o.MaxSize) ?? ResolutionChoices[0];
            SelectedBitrate = BitrateChoices.FirstOrDefault(c => c.Value == o.VideoBitRateMbps) ?? BitrateChoices[0];
            SelectedFps = FpsChoices.FirstOrDefault(c => c.Value == o.MaxFps) ?? FpsChoices[0];
            ForwardAudio = o.ForwardAudio;
            UhidInput = o.UhidInput;
            AlwaysOnTop = o.AlwaysOnTop;
            Fullscreen = o.Fullscreen;
            StayAwake = o.StayAwake;
            RecordToFile = o.RecordToFile;
        }
        finally
        {
            _loadingOptions = false;
        }
    }

    private void SaveOptions()
    {
        if (_loadingOptions)
        {
            return;
        }

        var options = CurrentOptions;
        Task.Run(() => _settings.Update(s => s.Options = options));
    }

    // ---------- Status & log ----------

    public ObservableCollection<LogLineItem> LogLines { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool StatusOk { get; set; }

    [ObservableProperty]
    public partial string ScrcpyVersionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool InjectionBlocked { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMirroringCommand))]
    public partial bool ToolsReady { get; set; }

    /// <summary>Diminta VM; View menampilkan jendela panduan HyperOS.</summary>
    public event Action? HyperOsGuideRequested;

    /// <summary>Pesan singkat untuk ditampilkan View (MessageBox).</summary>
    public event Action<string>? MessageRequested;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        UpdateStatus();
        if (!_tools.Current.HasAdb)
        {
            return;
        }

        try
        {
            var server = await _adb.StartServerAsync();
            _adbServerOk = server.Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Gagal menjalankan server adb");
            _adbServerOk = false;
        }

        UpdateStatus();
        _watcher.Start();
        _mdns.Start();

        if (_tools.Current.HasScrcpy)
        {
            try
            {
                var version = await _sessions.GetVersionAsync();
                ScrcpyVersionText = version is null ? string.Empty : Strings.Format("Main_ScrcpyVersion", version);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Gagal membaca versi scrcpy");
            }
        }
    }

    private void UpdateStatus()
    {
        var tools = _tools.Current;
        ToolsReady = tools.IsComplete;
        if (!tools.IsComplete)
        {
            StatusText = Strings.Get("Main_StatusToolsMissing");
            StatusOk = false;
        }
        else if (_initialized && !_adbServerOk)
        {
            StatusText = Strings.Get("Main_StatusAdbFailed");
            StatusOk = false;
        }
        else if (Devices.Any(d => d.IsReady))
        {
            StatusText = Strings.Get("Main_StatusAdbReady");
            StatusOk = true;
        }
        else
        {
            StatusText = Strings.Get("Main_StatusWaiting");
            StatusOk = false;
        }
    }

    private void OnLogEntry(LogEntry entry) => Post(() => AppendLog(entry));

    private void AppendLog(LogEntry entry)
    {
        if (entry.Level < LogLevel.Information)
        {
            return;
        }

        var problem = entry.Level >= LogLevel.Warning;
        foreach (var line in entry.Message.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0)
            {
                continue;
            }

            LogLines.Add(new LogLineItem(text, problem));
        }

        while (LogLines.Count > MaxLogLines)
        {
            LogLines.RemoveAt(0);
        }
    }

    // ---------- Daftar perangkat ----------

    private void OnDevicesChanged(IReadOnlyList<Device> devices) => Post(() => ApplyDevices(devices));

    private void ApplyDevices(IReadOnlyList<Device> devices)
    {
        var bySerial = Devices.ToDictionary(d => d.Serial, StringComparer.Ordinal);
        var ordered = new List<DeviceItemViewModel>();
        foreach (var device in devices)
        {
            if (bySerial.TryGetValue(device.Serial, out var existing))
            {
                existing.Update(device);
                ordered.Add(existing);
            }
            else
            {
                var item = new DeviceItemViewModel(device);
                if (_detailsCache.TryGetValue(device.Serial, out var cached))
                {
                    item.SetDetails(cached);
                }

                ordered.Add(item);
            }
        }

        // Sinkronkan koleksi tanpa mengganggu pilihan.
        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            if (!ordered.Contains(Devices[i]))
            {
                Devices.RemoveAt(i);
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            var currentIndex = Devices.IndexOf(item);
            if (currentIndex < 0)
            {
                Devices.Insert(i, item);
            }
            else if (currentIndex != i)
            {
                Devices.Move(currentIndex, i);
            }
        }

        foreach (var item in Devices)
        {
            item.IsMirroring = _sessions.Find(item.Serial) is not null;
            item.Refresh();
            if (item.IsReady && item.Details is null)
            {
                _ = FetchDetailsAsync(item);
            }
        }

        HasDevices = Devices.Count > 0;
        ApplyMdns();
        var ready = Devices.Count(d => d.IsReady);
        DeviceCountText = Devices.Count == 0
            ? Strings.Get("Main_DeviceCountSearching")
            : Strings.Format("Main_DeviceCount", ready);

        if (SelectedDevice is null || !Devices.Contains(SelectedDevice))
        {
            var last = _settings.Current.LastDeviceSerial;
            SelectedDevice = Devices.FirstOrDefault(d => d.IsReady && d.Serial == last)
                             ?? (ready == 1 ? Devices.First(d => d.IsReady) : null);
        }
        else
        {
            OnPropertyChanged(nameof(IsDeviceSelected));
            StartMirroringCommand.NotifyCanExecuteChanged();
        }

        UpdateStatus();
    }

    partial void OnSelectedDeviceChanged(DeviceItemViewModel? value)
    {
        if (value is { IsReady: true })
        {
            var serial = value.Serial;
            Task.Run(() => _settings.Update(s => s.LastDeviceSerial = serial));
        }
    }

    private async Task FetchDetailsAsync(DeviceItemViewModel item)
    {
        if (!_detailsInFlight.Add(item.Serial))
        {
            return;
        }

        try
        {
            var details = await _adb.GetDeviceDetailsAsync(item.Serial);
            _detailsCache[item.Serial] = details;
            Post(() =>
            {
                item.SetDetails(details);
                if (ReferenceEquals(item, SelectedDevice))
                {
                    OnPropertyChanged(nameof(SelectedDevice));
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Gagal membaca getprop {Serial}", item.Serial);
        }
        finally
        {
            _detailsInFlight.Remove(item.Serial);
        }
    }

    // ---------- Sesi scrcpy ----------

    private void OnSessionChanged(ScrcpySession session) => Post(() =>
    {
        foreach (var item in Devices)
        {
            item.IsMirroring = _sessions.Find(item.Serial) is not null;
            item.Refresh();
        }

        InjectionBlocked = _sessions.Sessions.Any(s => s.InjectionBlocked);
        OnPropertyChanged(nameof(IsMirroringSelected));
        OnPropertyChanged(nameof(StartButtonText));
        StartMirroringCommand.NotifyCanExecuteChanged();
    });

    private bool CanStartMirroring() => ToolsReady && SelectedDevice is { IsReady: true };

    [RelayCommand(CanExecute = nameof(CanStartMirroring))]
    private void StartMirroring()
    {
        var item = SelectedDevice;
        if (item is null)
        {
            return;
        }

        var existing = _sessions.Find(item.Serial);
        if (existing is not null)
        {
            existing.Stop();
            return;
        }

        try
        {
            var session = _sessions.Start(CurrentOptions, item.Device, item.DisplayName);
            session.InjectionBlockedDetected += _ => Post(() => InjectionBlocked = true);
            _logger.LogInformation("{Command}", session.Plan.ToCommandLine());
        }
        catch (Exception ex) when (ex is InvalidOperationException or ToolNotFoundException)
        {
            _logger.LogError(ex, "Gagal memulai scrcpy");
            MessageRequested?.Invoke(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        try
        {
            await _watcher.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Refresh perangkat gagal");
        }
    }

    [RelayCommand]
    private async Task RestartAdbAsync()
    {
        if (!_tools.Current.HasAdb)
        {
            MessageRequested?.Invoke(Strings.Get("Main_StatusToolsMissing"));
            return;
        }

        try
        {
            await _adb.KillServerAsync();
            var result = await _adb.StartServerAsync();
            _adbServerOk = result.Success;
            if (!result.Success)
            {
                MessageRequested?.Invoke(result.Message.Length > 0 ? result.Message : Strings.Get("Main_StatusAdbFailed"));
            }

            UpdateStatus();
            await _watcher.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Restart adb gagal");
        }
    }

    [RelayCommand]
    private void CopyLog()
    {
        var sb = new StringBuilder();
        foreach (var line in LogLines)
        {
            sb.AppendLine(line.Text);
        }

        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException)
        {
            _logger.LogWarning(ex, "Clipboard sedang dipakai aplikasi lain");
        }
    }

    [RelayCommand]
    private void OpenRecordingsFolder()
    {
        try
        {
            Directory.CreateDirectory(_paths.RecordingsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_paths.RecordingsDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Tidak bisa membuka folder rekaman");
        }
    }

    [RelayCommand]
    private void ShowHyperOsGuide() => HyperOsGuideRequested?.Invoke();

    // ---------- Koneksi nirkabel (Fase 3) ----------

    /// <summary>HP di jaringan (mDNS) yang belum tersambung ke adb; ditampilkan dengan tombol Sambung.</summary>
    public ObservableCollection<MdnsCandidateViewModel> MdnsCandidates { get; } = [];

    [ObservableProperty]
    public partial bool HasMdnsCandidates { get; set; }

    /// <summary>View membuka dialog Pair wireless; setelah tertutup, View memanggil <see cref="AfterWirelessDialogAsync"/>.</summary>
    public event Action? PairWirelessRequested;

    public event Action? ConnectIpRequested;

    [RelayCommand]
    private void PairWireless()
    {
        if (!_tools.Current.HasAdb)
        {
            MessageRequested?.Invoke(Strings.Get("Main_StatusToolsMissing"));
            return;
        }

        PairWirelessRequested?.Invoke();
    }

    [RelayCommand]
    private void ConnectViaIp()
    {
        if (!_tools.Current.HasAdb)
        {
            MessageRequested?.Invoke(Strings.Get("Main_StatusToolsMissing"));
            return;
        }

        ConnectIpRequested?.Invoke();
    }

    /// <summary>Setelah dialog pair/connect sukses: refresh daftar dan pilih perangkat barunya.</summary>
    public async Task AfterWirelessDialogAsync(string? connectedSerialOrAddress)
    {
        var devices = await _watcher.RefreshAsync();
        await _mdns.RefreshAsync();
        if (connectedSerialOrAddress is null)
        {
            return;
        }

        var match = devices.FirstOrDefault(d => d.IsReady && (d.Serial == connectedSerialOrAddress || d.Serial.StartsWith(connectedSerialOrAddress, StringComparison.OrdinalIgnoreCase)));
        if (match is not null)
        {
            SelectedDevice = Devices.FirstOrDefault(i => i.Serial == match.Serial) ?? SelectedDevice;
        }
    }

    [RelayCommand]
    private async Task EnableWifiFromUsbAsync()
    {
        var usb = SelectedDevice is { IsReady: true, Device.Transport: DeviceTransport.Usb } sel
            ? sel
            : Devices.FirstOrDefault(d => d.IsReady && d.Device.Transport == DeviceTransport.Usb);
        if (usb is null)
        {
            MessageRequested?.Invoke(Strings.Get("Wifi_NoUsbDevice"));
            return;
        }

        _logger.LogInformation("{Message}", Strings.Format("Wifi_Working", usb.DisplayName));
        try
        {
            var outcome = await _wireless.EnableWifiFromUsbAsync(usb.Serial);
            await _watcher.RefreshAsync();
            if (outcome.Success)
            {
                MessageRequested?.Invoke(Strings.Format("Wifi_Success", outcome.Address));
                await AfterWirelessDialogAsync(outcome.Address);
            }
            else
            {
                MessageRequested?.Invoke(Strings.Format("Wifi_Failed", outcome.Message));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Aktifkan Wi-Fi dari USB gagal");
            MessageRequested?.Invoke(Strings.Format("Wifi_Failed", ex.Message));
        }
    }

    [RelayCommand]
    private async Task ConnectMdnsAsync(MdnsCandidateViewModel? candidate)
    {
        if (candidate is null || candidate.IsConnecting)
        {
            return;
        }

        candidate.IsConnecting = true;
        try
        {
            var result = await _wireless.ConnectAsync(candidate.Address);
            if (result.Success)
            {
                await AfterWirelessDialogAsync(result.Address ?? candidate.Address);
            }
            else
            {
                MessageRequested?.Invoke(Strings.Format("Mdns_Failed", candidate.Address, result.Message));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sambung mDNS gagal");
            MessageRequested?.Invoke(Strings.Format("Mdns_Failed", candidate.Address, ex.Message));
        }
        finally
        {
            candidate.IsConnecting = false;
        }
    }

    private void OnMdnsChanged(IReadOnlyList<MdnsService> services) => Post(ApplyMdns);

    /// <summary>Tampilkan layanan mDNS yang belum ada di daftar adb (serial mDNS diawali nama layanan, atau IP:port sama).</summary>
    private void ApplyMdns()
    {
        var services = _mdns.Services;
        var known = Devices.Select(d => d.Serial).ToList();
        var wanted = services
            .Where(s => !known.Any(k => k.StartsWith(s.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(k, s.Address, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        for (var i = MdnsCandidates.Count - 1; i >= 0; i--)
        {
            if (!wanted.Any(s => s.Name == MdnsCandidates[i].Name && s.Address == MdnsCandidates[i].Address))
            {
                MdnsCandidates.RemoveAt(i);
            }
        }

        foreach (var s in wanted)
        {
            if (!MdnsCandidates.Any(c => c.Name == s.Name && c.Address == s.Address))
            {
                MdnsCandidates.Add(new MdnsCandidateViewModel(s));
            }
        }

        HasMdnsCandidates = MdnsCandidates.Count > 0;
    }

    private void Post(Action action) => _ui.Post(_ => action(), null);

    public void Dispose()
    {
        _logBuffer.EntryAdded -= OnLogEntry;
        _watcher.DevicesChanged -= OnDevicesChanged;
        _sessions.SessionStarted -= OnSessionChanged;
        _sessions.SessionEnded -= OnSessionChanged;
        _mdns.ServicesChanged -= OnMdnsChanged;
    }
}
