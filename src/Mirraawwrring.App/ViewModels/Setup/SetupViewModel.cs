using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Logging;
using Mirraawwrring.Core.Settings;
using Mirraawwrring.Core.Setup;
using Mirraawwrring.Core.Tools;
using Mirraawwrring.Core.Usb;

namespace Mirraawwrring.App.ViewModels.Setup;

public enum SetupStep
{
    Detect,
    Install,
    Verify,
    Phone,
    Done,
}

/// <summary>Satu lingkaran di stepper atas.</summary>
public sealed partial class StepIndicatorViewModel(int number, string title) : ObservableObject
{
    public int Number { get; } = number;
    public string Title { get; } = title;

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    public bool IsLast => Number == 4;
}

/// <summary>Layar 4 wizard (mockup Setup4-Selesai).</summary>
public sealed partial class DonePageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ScrcpyVersion { get; set; } = "?";

    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CheckUpdates { get; set; }
}

/// <summary>
/// Wizard setup (§6.1): Deteksi → Pasang → Verifikasi (+ layar HP) → Selesai. Muncul otomatis jika scrcpy/adb tidak ditemukan.
/// Footer (tombol kiri/sekunder/utama) berubah per langkah lewat <see cref="UpdateFooter"/>.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject, IDisposable
{
    private readonly IToolLocator _locator;
    private readonly IToolRegistry _tools;
    private readonly ISettingsStore _settings;
    private readonly VerificationService _verification;
    private readonly UsbWatcher _usbWatcher;
    private readonly ILogger<SetupViewModel> _logger;
    private readonly SynchronizationContext _ui;
    private SetupStep _phoneReturnStep = SetupStep.Verify;

    public SetupViewModel(
        IToolLocator locator,
        IToolRegistry tools,
        ISettingsStore settings,
        SetupService setup,
        VerificationService verification,
        IUsbProbe usbProbe,
        UsbWatcher usbWatcher,
        IAdbClient adb,
        InjectionProbe injection,
        LogBuffer logBuffer,
        ILogger<SetupViewModel> logger)
    {
        _locator = locator;
        _tools = tools;
        _settings = settings;
        _verification = verification;
        _usbWatcher = usbWatcher;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        Detect = new DetectPageViewModel(locator, tools, setup, usbProbe, adb, () => _settings.Current.CustomScrcpyPath);
        Install = new InstallPageViewModel(setup, logger);
        Verify = new VerifyPageViewModel(verification, logBuffer, logger);
        Phone = new PhonePageViewModel(usbProbe, adb, tools, injection);
        Done = new DonePageViewModel { CheckUpdates = _settings.Current.CheckScrcpyUpdates };

        Detect.Changed += UpdateFooter;
        Install.Changed += UpdateFooter;
        Install.Installed += OnInstalled;
        Verify.Changed += UpdateFooter;
        Verify.FixRequested += OnFixRequested;
        Phone.Changed += UpdateFooter;
        _usbWatcher.Changed += OnUsbChanged;

        Steps =
        [
            new StepIndicatorViewModel(1, Strings.Get("Setup_Step1")),
            new StepIndicatorViewModel(2, Strings.Get("Setup_Step2")),
            new StepIndicatorViewModel(3, Strings.Get("Setup_Step3")),
            new StepIndicatorViewModel(4, Strings.Get("Setup_Step4")),
        ];
        CurrentPage = Detect;
        UpdateFooter();
    }

    public DetectPageViewModel Detect { get; }
    public InstallPageViewModel Install { get; }
    public VerifyPageViewModel Verify { get; }
    public PhonePageViewModel Phone { get; }
    public DonePageViewModel Done { get; }

    public IReadOnlyList<StepIndicatorViewModel> Steps { get; }

    [ObservableProperty]
    public partial SetupStep Step { get; set; }

    [ObservableProperty]
    public partial object CurrentPage { get; set; }

    // Footer
    [ObservableProperty]
    public partial string LeftText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LeftVisible { get; set; }

    [ObservableProperty]
    public partial string SecondaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SecondaryVisible { get; set; }

    [ObservableProperty]
    public partial string PrimaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PrimaryEnabled { get; set; }

    /// <summary>true = setup selesai (tools lengkap), false = ditutup/dibatalkan.</summary>
    public event Action<bool>? CloseRequested;

    public event Action? OpenPairRequested;

    public event Action? OpenHyperOsGuideRequested;

    public bool IsComplete => _tools.Current.IsComplete;

    public async Task StartAsync()
    {
        _usbWatcher.Start();
        await GoToAsync(SetupStep.Detect);
    }

    // ---------- Navigasi ----------

    private async Task GoToAsync(SetupStep step)
    {
        if (Step == SetupStep.Phone && step != SetupStep.Phone)
        {
            Phone.Stop();
        }

        if (Step == SetupStep.Verify && step != SetupStep.Verify)
        {
            Verify.Cancel();
        }

        Step = step;
        CurrentPage = step switch
        {
            SetupStep.Detect => Detect,
            SetupStep.Install => Install,
            SetupStep.Verify => Verify,
            SetupStep.Phone => Phone,
            _ => Done,
        };
        UpdateSteps();
        UpdateFooter();

        switch (step)
        {
            case SetupStep.Detect:
                await Detect.RunAsync();
                break;
            case SetupStep.Verify:
                await Verify.RunAsync(skipPhone: false);
                if (Verify.ToolsPassed && Verify.PhoneFailed && Detect.PhoneStatus != PhoneUsbStatus.Ready)
                {
                    _phoneReturnStep = SetupStep.Verify;
                    await GoToAsync(SetupStep.Phone);
                }
                else if (Verify.AllPassed)
                {
                    await GoToAsync(SetupStep.Done);
                }

                break;
            case SetupStep.Phone:
                Phone.Start();
                break;
            case SetupStep.Done:
                PrepareDone();
                break;
        }
    }

    private void UpdateSteps()
    {
        var index = Step switch
        {
            SetupStep.Detect => 0,
            SetupStep.Install => 1,
            SetupStep.Verify or SetupStep.Phone => 2,
            _ => 3,
        };
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsDone = i < index;
            Steps[i].IsCurrent = i == index;
            Steps[i].IsFailed = i == 2 && Step == SetupStep.Verify && Verify.HasRun && !Verify.IsRunning && Verify.FailedCount > 0;
        }
    }

    private void UpdateFooter()
    {
        switch (Step)
        {
            case SetupStep.Detect:
                SetFooter(Strings.Get("Setup_Recheck"), true, string.Empty, false,
                    Detect.ToolsComplete ? Strings.Get("Setup_NextVerify") : Strings.Get("Setup_NextInstall"), !Detect.IsBusy);
                break;
            case SetupStep.Install:
                SetFooter(Strings.Get("Setup_Back"), !Install.IsInstalling,
                    Strings.Get("Install_Cancel"), Install.IsInstalling && Install.IsDownloading,
                    Install.IsInstalling ? Strings.Format("Install_Progress", (int)Install.Percent) : Strings.Get("Install_Start"),
                    Install.CanInstall);
                break;
            case SetupStep.Verify:
                var failed = Verify.HasRun && !Verify.IsRunning && Verify.FailedCount > 0;
                SetFooter(failed ? Strings.Get("Verify_CopyLog") : Strings.Get("Setup_Back"), true,
                    failed ? Strings.Get("Verify_BackToInstall") : Strings.Get("Verify_SkipPhone"), failed || Verify.IsRunning,
                    Verify.IsRunning ? Strings.Format("Verify_Progress", Verify.Completed, Verify.Total)
                        : failed ? Strings.Get("Setup_Recheck") : Strings.Get("Setup_Next"),
                    !Verify.IsRunning);
                break;
            case SetupStep.Phone:
                SetFooter(Strings.Get("Phone_WifiOnly"), true, Strings.Get("Setup_Skip"), true, Strings.Get("Setup_Next"), Phone.IsReady);
                break;
            case SetupStep.Done:
                SetFooter(string.Empty, false, string.Empty, false, Strings.Get("Done_Open"), true);
                break;
        }

        UpdateSteps();
    }

    private void SetFooter(string left, bool leftVisible, string secondary, bool secondaryVisible, string primary, bool primaryEnabled)
    {
        LeftText = left;
        LeftVisible = leftVisible;
        SecondaryText = secondary;
        SecondaryVisible = secondaryVisible;
        PrimaryText = primary;
        PrimaryEnabled = primaryEnabled;
    }

    [RelayCommand]
    private async Task LeftAsync()
    {
        switch (Step)
        {
            case SetupStep.Detect:
                await Detect.RunAsync();
                break;
            case SetupStep.Install:
                await GoToAsync(SetupStep.Detect);
                break;
            case SetupStep.Verify:
                if (Verify.HasRun && !Verify.IsRunning && Verify.FailedCount > 0)
                {
                    Verify.CopyLogCommand.Execute(null);
                }
                else
                {
                    await GoToAsync(SetupStep.Install);
                }

                break;
            case SetupStep.Phone:
                // Pakai Wi-Fi saja: lewati cek HP, selesai, lalu buka dialog Pair.
                await FinishSkippingPhoneAsync(openPair: true);
                break;
        }
    }

    [RelayCommand]
    private async Task SecondaryAsync()
    {
        switch (Step)
        {
            case SetupStep.Install:
                Install.CancelInstallCommand.Execute(null);
                break;
            case SetupStep.Verify:
                if (Verify.HasRun && !Verify.IsRunning && Verify.FailedCount > 0)
                {
                    await GoToAsync(SetupStep.Install);
                }
                else
                {
                    Verify.Cancel();
                    await RunVerifySkippingPhoneAsync();
                }

                break;
            case SetupStep.Phone:
                if (_phoneReturnStep == SetupStep.Detect)
                {
                    await GoToAsync(SetupStep.Detect);
                }
                else
                {
                    await FinishSkippingPhoneAsync(openPair: false);
                }

                break;
        }
    }

    [RelayCommand]
    private async Task PrimaryAsync()
    {
        switch (Step)
        {
            case SetupStep.Detect:
                await GoToAsync(Detect.ToolsComplete ? SetupStep.Verify : SetupStep.Install);
                break;
            case SetupStep.Install:
                await Install.InstallAsync();
                break;
            case SetupStep.Verify:
                if (Verify.AllPassed || (Verify.ToolsPassed && !Verify.IsRunning && Verify.FailedCount == 0))
                {
                    await GoToAsync(SetupStep.Done);
                }
                else
                {
                    await GoToAsync(SetupStep.Verify);
                }

                break;
            case SetupStep.Phone:
                await GoToAsync(SetupStep.Verify);
                break;
            case SetupStep.Done:
                _settings.Update(s => s.CheckScrcpyUpdates = Done.CheckUpdates);
                CloseRequested?.Invoke(IsComplete);
                break;
        }
    }

    private async Task RunVerifySkippingPhoneAsync()
    {
        Step = SetupStep.Verify;
        CurrentPage = Verify;
        UpdateFooter();
        await Verify.RunAsync(skipPhone: true);
        if (Verify.ToolsPassed)
        {
            await GoToAsync(SetupStep.Done);
        }
    }

    private async Task FinishSkippingPhoneAsync(bool openPair)
    {
        Phone.Stop();
        await GoToAsync(SetupStep.Done);
        if (openPair)
        {
            OpenPairRequested?.Invoke();
        }
    }

    /// <summary>Link "Cara mengaktifkan" di layar Deteksi.</summary>
    [RelayCommand]
    private async Task ShowPhoneGuideAsync()
    {
        _phoneReturnStep = SetupStep.Detect;
        await GoToAsync(SetupStep.Phone);
    }

    [RelayCommand]
    private void OpenWirelessCard()
    {
        CloseRequested?.Invoke(IsComplete);
        OpenPairRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenUsbCard() => CloseRequested?.Invoke(IsComplete);

    // ---------- Reaksi ----------

    private void OnInstalled()
    {
        var custom = Install.Method == InstallMethod.ExistingFolder ? Install.ResultScrcpyPath : null;
        _settings.Update(s => s.CustomScrcpyPath = custom);
        var location = _locator.Locate(custom);
        _tools.Update(location);
        _logger.LogInformation("Setelah pemasangan: scrcpy={Scrcpy} adb={Adb}", location.ScrcpyPath ?? "-", location.AdbPath ?? "-");
        _ = _ui.PostAsync(() => GoToAsync(SetupStep.Verify));
    }

    private async void OnFixRequested(VerifyRowViewModel row)
    {
        try
        {
            switch (row.Fix)
            {
                case FixAction.Redownload:
                    Install.Method = InstallMethod.Download;
                    await GoToAsync(SetupStep.Install);
                    break;
                case FixAction.KillOtherAdb when row.FixArgument is not null:
                    await _verification.KillOtherAdbAsync(row.FixArgument);
                    await GoToAsync(SetupStep.Verify);
                    break;
                case FixAction.EnableUsbDebugging:
                    _phoneReturnStep = SetupStep.Verify;
                    await GoToAsync(SetupStep.Phone);
                    break;
                case FixAction.SecuritySettings:
                    OpenHyperOsGuideRequested?.Invoke();
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Aksi perbaikan gagal");
        }
    }

    private void OnUsbChanged(UsbProbeResult result)
    {
        if (Step == SetupStep.Detect)
        {
            _ = _ui.PostAsync(() => Detect.RefreshPhoneAsync());
        }
    }

    private void PrepareDone()
    {
        var location = _tools.Current;
        Done.ScrcpyVersion = Verify.ScrcpyVersion ?? Install.Manifest?.Version ?? "?";
        Done.Location = location.ScrcpyDirectory ?? "-";
        Done.Subtitle = Verify.HasWarnings ? Strings.Get("Done_SubWarnings") : Strings.Get("Done_Sub");
    }

    public void Dispose()
    {
        _usbWatcher.Changed -= OnUsbChanged;
        Phone.Dispose();
        Verify.Cancel();
    }
}

internal static class SynchronizationContextExtensions
{
    public static Task PostAsync(this SynchronizationContext context, Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        context.Post(async _ =>
        {
            try
            {
                await action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, null);
        return tcs.Task;
    }
}
