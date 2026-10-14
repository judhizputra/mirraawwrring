using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Setup;

namespace Mirraawwrring.App.ViewModels.Setup;

public enum InstallMethod
{
    Download,
    Winget,
    ExistingFolder,
}

/// <summary>Layar 2 wizard: unduh otomatis / winget / folder yang sudah ada (mockup Setup2-Pasang).</summary>
public sealed partial class InstallPageViewModel : ObservableObject
{
    private readonly SetupService _setup;
    private readonly ILogger _logger;
    private readonly SynchronizationContext _ui;
    private CancellationTokenSource? _cts;

    public InstallPageViewModel(SetupService setup, ILogger logger)
    {
        _setup = setup;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        try
        {
            Manifest = setup.LoadManifest();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _logger.LogError(ex, "Manifest scrcpy tidak bisa dibaca");
        }

        AutoDescription = Strings.Format("Install_AutoDesc", Manifest?.Version ?? "?");
    }

    public ScrcpyManifest? Manifest { get; }

    public string AutoDescription { get; }

    public string WingetCommand => Strings.Get("Install_WingetCommand");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadSelected))]
    [NotifyPropertyChangedFor(nameof(IsWingetSelected))]
    [NotifyPropertyChangedFor(nameof(IsFolderSelected))]
    public partial InstallMethod Method { get; set; } = InstallMethod.Download;

    public bool IsDownloadSelected
    {
        get => Method == InstallMethod.Download;
        set
        {
            if (value)
            {
                Method = InstallMethod.Download;
            }
        }
    }

    public bool IsWingetSelected
    {
        get => Method == InstallMethod.Winget;
        set
        {
            if (value)
            {
                Method = InstallMethod.Winget;
            }
        }
    }

    public bool IsFolderSelected
    {
        get => Method == InstallMethod.ExistingFolder;
        set
        {
            if (value)
            {
                Method = InstallMethod.ExistingFolder;
            }
        }
    }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    /// <summary>0..100, null = indeterminate.</summary>
    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string ProgressTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgressBytes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; set; }

    public ObservableCollection<string> WingetOutput { get; } = [];

    [ObservableProperty]
    public partial bool HasWingetOutput { get; set; }

    [ObservableProperty]
    public partial string? FolderPath { get; set; }

    [ObservableProperty]
    public partial string? FolderMessage { get; set; }

    [ObservableProperty]
    public partial bool FolderValid { get; set; }

    /// <summary>Path scrcpy.exe hasil pemasangan/pemilihan folder; dipakai wizard untuk Locate ulang dan menyimpan CustomScrcpyPath.</summary>
    public string? ResultScrcpyPath { get; private set; }

    public event Action? Changed;

    /// <summary>Pemasangan selesai sukses; wizard lanjut ke Verifikasi.</summary>
    public event Action? Installed;

    public bool CanInstall => !IsInstalling && Method switch
    {
        InstallMethod.Download => Manifest is not null,
        InstallMethod.Winget => true,
        InstallMethod.ExistingFolder => FolderValid,
        _ => false,
    };

    partial void OnMethodChanged(InstallMethod value) => Changed?.Invoke();
    partial void OnIsInstallingChanged(bool value) => Changed?.Invoke();
    partial void OnFolderValidChanged(bool value) => Changed?.Invoke();
    partial void OnPercentChanged(double value) => Changed?.Invoke();

    public void SetFolder(string? folder)
    {
        FolderPath = folder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            FolderValid = false;
            FolderMessage = null;
            return;
        }

        var validation = SetupService.ValidateFolder(folder);
        FolderValid = validation.IsValid;
        FolderMessage = validation.IsValid
            ? Strings.Format("Install_FolderOk", validation.ScrcpyPath)
            : Strings.Format("Install_FolderInvalid", string.Join(", ", validation.MissingFiles));
        if (validation.IsValid)
        {
            Method = InstallMethod.ExistingFolder;
        }
    }

    [RelayCommand]
    public async Task InstallAsync()
    {
        if (!CanInstall)
        {
            return;
        }

        Error = null;
        IsInstalling = true;
        _cts = new CancellationTokenSource();
        try
        {
            switch (Method)
            {
                case InstallMethod.Download:
                    await DownloadAsync(_cts.Token);
                    break;
                case InstallMethod.Winget:
                    await WingetAsync(_cts.Token);
                    break;
                case InstallMethod.ExistingFolder:
                    ResultScrcpyPath = SetupService.ValidateFolder(FolderPath!).ScrcpyPath;
                    Installed?.Invoke();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            ProgressTitle = string.Empty;
            ProgressBytes = string.Empty;
        }
        catch (Exception ex) when (ex is ChecksumMismatchException or HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "Pemasangan gagal");
            Error = Strings.Format("Install_Failed", ex.Message);
        }
        finally
        {
            IsInstalling = false;
            IsDownloading = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelInstall() => _cts?.Cancel();

    private async Task DownloadAsync(CancellationToken ct)
    {
        var manifest = Manifest!;
        IsDownloading = true;
        Percent = 0;
        ProgressTitle = Strings.Format("Install_Downloading", manifest.FileName);
        ProgressBytes = string.Empty;
        var progress = new Progress<SetupProgress>(p => _ui.Post(_ => ApplyProgress(p), null));
        var target = await _setup.DownloadAndInstallAsync(manifest, progress, ct);
        ResultScrcpyPath = Path.Combine(target, "scrcpy.exe");
        Installed?.Invoke();
    }

    private void ApplyProgress(SetupProgress p)
    {
        switch (p.Stage)
        {
            case SetupStage.Downloading:
                Percent = (p.Fraction ?? 0) * 100;
                var mb = (p.BytesReceived / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture);
                var total = p.TotalBytes is { } t ? (t / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture) : "?";
                ProgressBytes = Strings.Format("Install_Bytes", mb, total);
                break;
            case SetupStage.Verifying:
                Percent = 100;
                ProgressTitle = Strings.Get("Install_StageVerifying");
                break;
            case SetupStage.Extracting:
                ProgressTitle = Strings.Get("Install_StageExtracting");
                break;
            case SetupStage.Unblocking:
                ProgressTitle = Strings.Get("Install_StageUnblocking");
                break;
            case SetupStage.Done:
                ProgressTitle = Strings.Get("Install_StageDone");
                break;
        }
    }

    private async Task WingetAsync(CancellationToken ct)
    {
        WingetOutput.Clear();
        HasWingetOutput = true;
        int code;
        try
        {
            code = await _setup.InstallViaWingetAsync(line => _ui.Post(_ => AppendWinget(line), null), ct);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Error = Strings.Get("Install_WingetNotFound");
            return;
        }

        if (code != 0)
        {
            Error = Strings.Format("Install_WingetFailed", code);
            return;
        }

        ResultScrcpyPath = null; // ToolLocator akan menemukan symlink winget.
        Installed?.Invoke();
    }

    private void AppendWinget(string line)
    {
        // winget menggambar ulang progress bar dengan \r; ambil segmen terakhir.
        var text = line.Split('\r')[^1].Trim();
        if (text.Length == 0)
        {
            return;
        }

        WingetOutput.Add(text);
        while (WingetOutput.Count > 200)
        {
            WingetOutput.RemoveAt(0);
        }
    }
}
