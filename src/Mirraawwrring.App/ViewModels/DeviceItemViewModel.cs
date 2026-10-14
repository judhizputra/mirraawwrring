using CommunityToolkit.Mvvm.ComponentModel;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.App.ViewModels;

public enum BadgeKind
{
    Connected,
    Warning,
    Muted,
    Mirroring,
}

/// <summary>Satu kartu di daftar perangkat.</summary>
public sealed partial class DeviceItemViewModel : ObservableObject
{
    public DeviceItemViewModel(Device device)
    {
        Device = device;
        Serial = device.Serial;
        Refresh();
    }

    public string Serial { get; }

    public Device Device { get; private set; }

    public DeviceDetails? Details { get; private set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AndroidText { get; set; }

    [ObservableProperty]
    public partial string BadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial BadgeKind BadgeKind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool IsMirroring { get; set; }

    public bool IsReady => Device.IsReady;

    public bool IsWireless => Device.IsWireless;

    public void Update(Device device)
    {
        Device = device;
        Refresh();
    }

    public void SetDetails(DeviceDetails details)
    {
        Details = details;
        Refresh();
    }

    public void Refresh()
    {
        var d = Device;
        var model = Details?.DisplayName is { Length: > 0 } name ? name : d.DisplayName;
        DisplayName = model;
        AndroidText = Details?.AndroidVersion is { Length: > 0 } v ? Strings.Format("Device_Android", v) : null;

        Subtitle = (d.Transport, d.State) switch
        {
            (DeviceTransport.WifiIp, _) => Strings.Format("Device_Subtitle_Wifi", d.Serial),
            (DeviceTransport.WifiMdns, _) => Strings.Get("Device_Subtitle_WifiMdns"),
            (DeviceTransport.Emulator, _) => Strings.Format("Device_Subtitle_Emulator", d.Serial),
            (_, DeviceState.Unauthorized) => Strings.Get("Device_Subtitle_UsbUnauthorized"),
            (_, DeviceState.Offline) => Strings.Get("Device_Subtitle_UsbOffline"),
            _ => Strings.Format("Device_Subtitle_Usb", d.Serial),
        };

        if (IsMirroring)
        {
            BadgeText = Strings.Get("Device_Badge_Mirroring");
            BadgeKind = BadgeKind.Mirroring;
            OnPropertyChanged(nameof(IsReady));
            return;
        }

        (BadgeText, BadgeKind) = d.State switch
        {
            DeviceState.Device => (Strings.Get("Device_Badge_Connected"), BadgeKind.Connected),
            DeviceState.Unauthorized => (Strings.Get("Device_Badge_Unauthorized"), BadgeKind.Warning),
            DeviceState.Offline => (Strings.Get("Device_Badge_Offline"), BadgeKind.Muted),
            DeviceState.NoPermissions => (Strings.Get("Device_Badge_NoPermissions"), BadgeKind.Warning),
            DeviceState.Recovery => (Strings.Get("Device_Badge_Recovery"), BadgeKind.Muted),
            DeviceState.Sideload => (Strings.Get("Device_Badge_Sideload"), BadgeKind.Muted),
            _ => (d.RawState, BadgeKind.Muted),
        };
        OnPropertyChanged(nameof(IsReady));
    }
}
