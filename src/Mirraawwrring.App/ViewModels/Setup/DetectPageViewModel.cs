using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Setup;
using Mirraawwrring.Core.Tools;
using Mirraawwrring.Core.Usb;

namespace Mirraawwrring.App.ViewModels.Setup;

public enum RowKind
{
    Pending,
    Ok,
    Warning,
    Failed,
    Skipped,
}

/// <summary>Satu baris di daftar pemeriksaan (layar Deteksi dan Status HP).</summary>
public sealed partial class DetectRowViewModel(string title, string description) : ObservableObject
{
    public string Title { get; } = title;

    [ObservableProperty]
    public partial string Description { get; set; } = description;

    [ObservableProperty]
    public partial string StatusText { get; set; } = Strings.Get("Detect_Checking");

    [ObservableProperty]
    public partial RowKind Kind { get; set; } = RowKind.Pending;

    [ObservableProperty]
    public partial bool ShowHowTo { get; set; }

    public void Set(RowKind kind, string status, string? description = null)
    {
        Kind = kind;
        StatusText = status;
        if (description is not null)
        {
            Description = description;
        }
    }
}

/// <summary>Layar 1 wizard: deteksi scrcpy, ADB, internet, HP · USB debugging (mockup Setup1-Deteksi).</summary>
public sealed partial class DetectPageViewModel : ObservableObject
{
    private readonly IToolLocator _locator;
    private readonly IToolRegistry _tools;
    private readonly SetupService _setup;
    private readonly IUsbProbe _usb;
    private readonly IAdbClient _adb;
    private readonly Func<string?> _customPath;

    public DetectPageViewModel(IToolLocator locator, IToolRegistry tools, SetupService setup, IUsbProbe usb, IAdbClient adb, Func<string?> customPath)
    {
        _locator = locator;
        _tools = tools;
        _setup = setup;
        _usb = usb;
        _adb = adb;
        _customPath = customPath;
        Scrcpy = new DetectRowViewModel(Strings.Get("Detect_Scrcpy"), Strings.Get("Detect_ScrcpyDesc"));
        Adb = new DetectRowViewModel(Strings.Get("Detect_Adb"), Strings.Get("Detect_AdbDesc"));
        Internet = new DetectRowViewModel(Strings.Get("Detect_Internet"), Strings.Get("Detect_InternetDesc"));
        Phone = new DetectRowViewModel(Strings.Get("Detect_Phone"), Strings.Get("Detect_PhoneDescNone"));
    }

    public DetectRowViewModel Scrcpy { get; }
    public DetectRowViewModel Adb { get; }
    public DetectRowViewModel Internet { get; }
    public DetectRowViewModel Phone { get; }

    public IReadOnlyList<DetectRowViewModel> Rows => [Scrcpy, Adb, Internet, Phone];

    [ObservableProperty]
    public partial bool ToolsComplete { get; set; }

    [ObservableProperty]
    public partial bool HasInternet { get; set; }

    [ObservableProperty]
    public partial PhoneUsbStatus PhoneStatus { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ToolLocation Location { get; private set; } = ToolLocation.NotFound;

    public event Action? Changed;

    public async Task RunAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            Location = _locator.Locate(_customPath());
            _tools.Update(Location);
            ToolsComplete = Location.IsComplete;
            Scrcpy.Set(Location.HasScrcpy ? RowKind.Ok : RowKind.Warning,
                Location.HasScrcpy ? Strings.Get("Detect_Found") : Strings.Get("Detect_NotFound"),
                Location.HasScrcpy ? Location.ScrcpyPath : Strings.Get("Detect_ScrcpyDesc"));
            Adb.Set(Location.HasAdb ? RowKind.Ok : RowKind.Warning,
                Location.HasAdb ? Strings.Get("Detect_Found") : Strings.Get("Detect_NotFound"),
                Location.HasAdb ? Location.AdbPath : Strings.Get("Detect_AdbDesc"));
            Changed?.Invoke();

            var internetTask = CheckInternetAsync(ct);
            await RefreshPhoneAsync(ct);
            await internetTask;
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    private async Task CheckInternetAsync(CancellationToken ct)
    {
        Internet.Set(RowKind.Pending, Strings.Get("Detect_Checking"));
        bool ok;
        try
        {
            ok = await _setup.HasInternetAsync(_setup.LoadManifest(), ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            ok = false;
        }

        HasInternet = ok;
        Internet.Set(ok ? RowKind.Ok : RowKind.Warning, ok ? Strings.Get("Detect_Available") : Strings.Get("Detect_Unavailable"));
    }

    /// <summary>Perbarui baris HP dari UsbProbe + adb (dipanggil ulang saat USB berubah).</summary>
    public async Task RefreshPhoneAsync(CancellationToken ct = default)
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

        PhoneStatus = PhoneStatusEvaluator.Evaluate(usb, devices);
        var brand = usb.Brand ?? "HP";
        var model = devices.FirstOrDefault(d => d.IsReady)?.DisplayName ?? brand;
        Phone.ShowHowTo = PhoneStatus is PhoneUsbStatus.ConnectedDebuggingOff or PhoneUsbStatus.NotConnected or PhoneUsbStatus.DebuggingOnButAdbBlind;
        switch (PhoneStatus)
        {
            case PhoneUsbStatus.Ready:
                Phone.Set(RowKind.Ok, Strings.Get("Detect_PhoneReady"), Strings.Format("Detect_PhoneDescReady", model));
                break;
            case PhoneUsbStatus.Unauthorized:
                Phone.Set(RowKind.Warning, Strings.Get("Detect_PhoneUnauthorized"), Strings.Get("Detect_PhoneDescUnauthorized"));
                break;
            case PhoneUsbStatus.DebuggingOnButAdbBlind:
                Phone.Set(RowKind.Warning, Strings.Get("Detect_PhoneBlind"), Strings.Get("Detect_PhoneDescBlind"));
                break;
            case PhoneUsbStatus.ConnectedDebuggingOff:
                Phone.Set(RowKind.Warning, Strings.Get("Detect_PhoneOff"), Strings.Format("Detect_PhoneDescOff", brand));
                break;
            default:
                Phone.Set(RowKind.Warning, Strings.Get("Detect_PhoneNone"), Strings.Get("Detect_PhoneDescNone"));
                break;
        }

        Changed?.Invoke();
    }
}
