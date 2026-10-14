using CommunityToolkit.Mvvm.ComponentModel;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Tools;
using Mirraawwrring.Core.Usb;

namespace Mirraawwrring.App.ViewModels.Setup;

public sealed record NumberedStep(int Number, string Text)
{
    public string Label => $"{Number}.";
}

/// <summary>Layar 3c wizard: USB debugging belum aktif, langkah HyperOS + status HP yang diperbarui tiap 2 detik.</summary>
public sealed partial class PhonePageViewModel : ObservableObject, IDisposable
{
    private readonly IUsbProbe _usb;
    private readonly IAdbClient _adb;
    private readonly IToolRegistry _tools;
    private readonly InjectionProbe _injection;
    private readonly SynchronizationContext _ui;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PhonePageViewModel(IUsbProbe usb, IAdbClient adb, IToolRegistry tools, InjectionProbe injection)
    {
        _usb = usb;
        _adb = adb;
        _tools = tools;
        _injection = injection;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        UsbRow = new DetectRowViewModel(Strings.Get("Phone_StatusUsb"), Strings.Get("Phone_StatusUsbNone"));
        DebuggingRow = new DetectRowViewModel(Strings.Get("Phone_StatusDebugging"), Strings.Get("Phone_StatusDebuggingOff"));
        AuthRow = new DetectRowViewModel(Strings.Get("Phone_StatusAuth"), Strings.Get("Phone_StatusAuthWaiting"));
        ControlRow = new DetectRowViewModel(Strings.Get("Phone_StatusControl"), Strings.Get("Phone_StatusControlPending"));
        foreach (var r in Rows)
        {
            r.StatusText = string.Empty;
        }
    }

    public DetectRowViewModel UsbRow { get; }
    public DetectRowViewModel DebuggingRow { get; }
    public DetectRowViewModel AuthRow { get; }
    public DetectRowViewModel ControlRow { get; }

    public IReadOnlyList<DetectRowViewModel> Rows => [UsbRow, DebuggingRow, AuthRow, ControlRow];

    public IReadOnlyList<NumberedStep> Steps { get; } =
    [
        new(1, Strings.Get("Phone_Step1")), new(2, Strings.Get("Phone_Step2")), new(3, Strings.Get("Phone_Step3")),
        new(4, Strings.Get("Phone_Step4")), new(5, Strings.Get("Phone_Step5")),
    ];

    [ObservableProperty]
    public partial PhoneUsbStatus Status { get; set; }

    [ObservableProperty]
    public partial string Intro { get; set; } = Strings.Get("Phone_IntroNone");

    public bool IsReady => Status == PhoneUsbStatus.Ready;

    public event Action? Changed;

    public void Start()
    {
        if (_loop is { IsCompleted: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var usb = await _usb.ProbeAsync(ct);
        IReadOnlyList<Device> devices = [];
        if (_tools.Current.HasAdb)
        {
            try
            {
                devices = await _adb.GetDevicesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                devices = [];
            }
        }

        var status = PhoneStatusEvaluator.Evaluate(usb, devices);
        var ready = devices.FirstOrDefault(d => d.IsReady && d.Transport == DeviceTransport.Usb);
        InjectionStatus injection = InjectionStatus.Unknown;
        if (ready is not null)
        {
            injection = await _injection.ProbeAsync(ready.Serial, ct);
        }

        _ui.Post(_ => Apply(status, usb.Brand, ready, injection), null);
    }

    private void Apply(PhoneUsbStatus status, string? brand, Device? ready, InjectionStatus injection)
    {
        Status = status;
        Intro = status == PhoneUsbStatus.NotConnected ? Strings.Get("Phone_IntroNone") : Strings.Get("Phone_Intro");
        var connected = status != PhoneUsbStatus.NotConnected;
        UsbRow.Set(connected ? RowKind.Ok : RowKind.Pending, string.Empty, connected ? Strings.Format("Phone_StatusUsbDetail", ready?.DisplayName ?? brand ?? "HP") : Strings.Get("Phone_StatusUsbNone"));

        var (debugKind, debugText) = status switch
        {
            PhoneUsbStatus.Ready or PhoneUsbStatus.Unauthorized => (RowKind.Ok, Strings.Get("Phone_StatusDebuggingOn")),
            PhoneUsbStatus.DebuggingOnButAdbBlind => (RowKind.Warning, Strings.Get("Phone_StatusDebuggingBlind")),
            PhoneUsbStatus.ConnectedDebuggingOff => (RowKind.Warning, Strings.Get("Phone_StatusDebuggingOff")),
            _ => (RowKind.Pending, Strings.Get("Phone_StatusDebuggingOff")),
        };
        DebuggingRow.Set(debugKind, string.Empty, debugText);

        AuthRow.Set(status == PhoneUsbStatus.Ready ? RowKind.Ok : RowKind.Pending, string.Empty,
            status == PhoneUsbStatus.Ready ? Strings.Get("Phone_StatusAuthOk") : Strings.Get("Phone_StatusAuthWaiting"));

        var (ctrlKind, ctrlText) = injection switch
        {
            InjectionStatus.Allowed => (RowKind.Ok, Strings.Get("Phone_StatusControlOk")),
            InjectionStatus.Blocked => (RowKind.Warning, Strings.Get("Phone_StatusControlBlocked")),
            _ => (RowKind.Pending, Strings.Get("Phone_StatusControlPending")),
        };
        ControlRow.Set(ctrlKind, string.Empty, ctrlText);
        OnPropertyChanged(nameof(IsReady));
        Changed?.Invoke();
    }

    public void Dispose() => Stop();
}
