using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Logging;
using Mirraawwrring.Core.Setup;

namespace Mirraawwrring.App.ViewModels.Setup;

/// <summary>Satu baris pemeriksaan di layar Verifikasi; saat gagal ditampilkan sebagai kartu error dengan tombol perbaikan.</summary>
public sealed partial class VerifyRowViewModel(CheckId id, CheckGroup group, string title, string failedTitle, string failedDescription) : ObservableObject
{
    public CheckId Id { get; } = id;
    public CheckGroup Group { get; } = group;
    public string Title { get; } = title;
    public string FailedTitle { get; } = failedTitle;
    public string FailedDescription { get; } = failedDescription;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsOkRow))]
    public partial RowKind Kind { get; set; } = RowKind.Pending;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFix))]
    [NotifyPropertyChangedFor(nameof(FixText))]
    public partial FixAction Fix { get; set; }

    public string? FixArgument { get; set; }

    public bool IsFailed => Kind == RowKind.Failed;

    public bool IsOkRow => Kind != RowKind.Failed;

    public bool HasFix => Fix != FixAction.None;

    public string FixText => Fix switch
    {
        FixAction.Redownload => Strings.Get("Verify_FixRedownload"),
        FixAction.KillOtherAdb => Strings.Get("Verify_FixKillAdb"),
        FixAction.EnableUsbDebugging => Strings.Get("Verify_FixUsbDebugging"),
        FixAction.SecuritySettings => Strings.Get("Verify_FixSecurity"),
        _ => string.Empty,
    };

    public void Apply(CheckResult r)
    {
        Kind = r.Status switch
        {
            CheckStatus.Ok => RowKind.Ok,
            CheckStatus.Warning => RowKind.Warning,
            CheckStatus.Failed => RowKind.Failed,
            CheckStatus.Skipped => RowKind.Skipped,
            _ => RowKind.Pending,
        };
        Detail = r.Status == CheckStatus.Skipped ? Strings.Get("Verify_Skipped") : r.Detail;
        Fix = r.Fix;
        FixArgument = r.FixArgument;
    }

    public void Reset()
    {
        Kind = RowKind.Pending;
        Detail = string.Empty;
        Fix = FixAction.None;
        FixArgument = null;
    }
}

/// <summary>Layar 3 wizard: verifikasi scrcpy, ADB, HP (mockup Setup3-Verifikasi dan Setup3b-Gagal).</summary>
public sealed partial class VerifyPageViewModel : ObservableObject
{
    private readonly VerificationService _verification;
    private readonly LogBuffer _logBuffer;
    private readonly ILogger _logger;
    private readonly SynchronizationContext _ui;
    private CancellationTokenSource? _cts;

    public VerifyPageViewModel(VerificationService verification, LogBuffer logBuffer, ILogger logger)
    {
        _verification = verification;
        _logBuffer = logBuffer;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        Rows =
        [
            new VerifyRowViewModel(CheckId.ScrcpyFiles, CheckGroup.Scrcpy, Strings.Get("Verify_ScrcpyFiles"), Strings.Get("Verify_FilesFailedTitle"), string.Empty),
            new VerifyRowViewModel(CheckId.ScrcpyRuns, CheckGroup.Scrcpy, Strings.Get("Verify_ScrcpyRuns"), Strings.Get("Verify_ScrcpyRunsFailedTitle"), Strings.Get("Verify_ScrcpyRunsFailedDesc")),
            new VerifyRowViewModel(CheckId.AdbRuns, CheckGroup.Adb, Strings.Get("Verify_AdbRuns"), Strings.Get("Verify_ScrcpyRunsFailedTitle").Replace("scrcpy", "ADB"), string.Empty),
            new VerifyRowViewModel(CheckId.AdbServer, CheckGroup.Adb, Strings.Get("Verify_AdbServer"), Strings.Get("Verify_AdbServerFailedTitle"), Strings.Get("Verify_AdbServerFailedDesc")),
            new VerifyRowViewModel(CheckId.OtherAdb, CheckGroup.Adb, Strings.Get("Verify_NoOtherAdb"), string.Empty, string.Empty),
            new VerifyRowViewModel(CheckId.PhoneAuthorized, CheckGroup.Phone, Strings.Get("Verify_PhoneAuthorized"), Strings.Get("Verify_PhoneFailedTitle"), string.Empty),
            new VerifyRowViewModel(CheckId.PhoneInjection, CheckGroup.Phone, Strings.Get("Verify_PhoneInjection"), string.Empty, string.Empty),
        ];
    }

    public IReadOnlyList<VerifyRowViewModel> Rows { get; }

    public IEnumerable<VerifyRowViewModel> ScrcpyRows => Rows.Where(r => r.Group == CheckGroup.Scrcpy);
    public IEnumerable<VerifyRowViewModel> AdbRows => Rows.Where(r => r.Group == CheckGroup.Adb);
    public IEnumerable<VerifyRowViewModel> PhoneRows => Rows.Where(r => r.Group == CheckGroup.Phone);

    public ObservableCollection<LogLineItem> LogLines { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial int Completed { get; set; }

    [ObservableProperty]
    public partial int FailedCount { get; set; }

    [ObservableProperty]
    public partial bool HasRun { get; set; }

    public int Total => Rows.Count;

    public bool AllPassed => HasRun && !IsRunning && FailedCount == 0;

    public bool PhoneFailed => Rows.Any(r => r.Group == CheckGroup.Phone && r.Kind == RowKind.Failed);

    public bool ToolsPassed => HasRun && !IsRunning && Rows.Where(r => r.Group != CheckGroup.Phone).All(r => r.Kind != RowKind.Failed);

    public string? ScrcpyVersion => Rows[1].Kind == RowKind.Ok ? Rows[1].Detail.Replace("scrcpy ", string.Empty) : null;

    public bool HasWarnings => Rows.Any(r => r.Kind == RowKind.Warning);

    public event Action? Changed;

    /// <summary>Tombol perbaikan ditekan; wizard yang menangani (unduh ulang, hentikan adb lain, buka panduan).</summary>
    public event Action<VerifyRowViewModel>? FixRequested;

    public async Task RunAsync(bool skipPhone)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        foreach (var row in Rows)
        {
            row.Reset();
        }

        LogLines.Clear();
        Completed = 0;
        FailedCount = 0;
        IsRunning = true;
        HasRun = true;
        Changed?.Invoke();
        _logBuffer.EntryAdded += OnLog;
        try
        {
            var results = await _verification.RunAsync(skipPhone, r => _ui.Post(_ => Report(r), null), ct);
            FailedCount = results.Count(r => r.Status == CheckStatus.Failed);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Verifikasi gagal");
        }
        finally
        {
            _logBuffer.EntryAdded -= OnLog;
            IsRunning = false;
            Changed?.Invoke();
        }
    }

    public void Cancel() => _cts?.Cancel();

    private void Report(CheckResult r)
    {
        var row = Rows.First(x => x.Id == r.Id);
        if (r.Status == CheckStatus.Running)
        {
            row.Kind = RowKind.Pending;
            return;
        }

        row.Apply(r);
        Completed++;
        FailedCount = Rows.Count(x => x.Kind == RowKind.Failed);
        Changed?.Invoke();
    }

    private void OnLog(LogEntry entry)
    {
        if (entry.Level < Microsoft.Extensions.Logging.LogLevel.Information || entry.Category is "VerifyPageViewModel" or "SetupViewModel")
        {
            return;
        }

        _ui.Post(_ =>
        {
            foreach (var line in entry.Message.Split('\n'))
            {
                var text = line.TrimEnd('\r');
                if (text.Length > 0)
                {
                    LogLines.Add(new LogLineItem(text, entry.Level >= Microsoft.Extensions.Logging.LogLevel.Warning));
                }
            }
        }, null);
    }

    [RelayCommand]
    private void Fix(VerifyRowViewModel? row)
    {
        if (row is not null)
        {
            FixRequested?.Invoke(row);
        }
    }

    [RelayCommand]
    private void CopyLog()
    {
        try
        {
            System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, LogLines.Select(l => l.Text)));
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }
}
