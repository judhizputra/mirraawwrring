namespace Mirraawwrring.Core.Adb;

/// <summary>State dari kolom kedua <c>adb devices -l</c>.</summary>
public enum DeviceState
{
    /// <summary><c>device</c>: siap dipakai.</summary>
    Device,

    /// <summary><c>unauthorized</c>: popup izin di HP belum disetujui.</summary>
    Unauthorized,

    Offline,

    /// <summary><c>no permissions</c>: biasanya masalah driver/udev.</summary>
    NoPermissions,

    Recovery,

    Sideload,

    /// <summary>State lain (connecting, authorizing, bootloader, host, …); lihat <see cref="Device.RawState"/>.</summary>
    Unknown,
}

public enum DeviceTransport
{
    Usb,

    /// <summary>Serial berbentuk <c>IP:port</c> (adb connect / tcpip).</summary>
    WifiIp,

    /// <summary>Serial berbentuk <c>adb-XXXX._adb-tls-connect._tcp</c> (Wireless debugging via mDNS).</summary>
    WifiMdns,

    /// <summary>Serial <c>emulator-NNNN</c>.</summary>
    Emulator,
}

/// <summary>Satu baris dari <c>adb devices -l</c>.</summary>
public sealed record Device
{
    public required string Serial { get; init; }

    public required DeviceState State { get; init; }

    /// <summary>Teks state asli, berguna untuk state yang tidak dikenali.</summary>
    public required string RawState { get; init; }

    public required DeviceTransport Transport { get; init; }

    /// <summary><c>model:</c> dari adb, mis. <c>2407FPN8EG</c>; underscore sudah diganti spasi.</summary>
    public string? Model { get; init; }

    public string? Product { get; init; }

    /// <summary><c>device:</c> (nama kode perangkat).</summary>
    public string? DeviceCodename { get; init; }

    /// <summary><c>usb:</c> mis. <c>1-1</c>.</summary>
    public string? UsbPath { get; init; }

    public int? TransportId { get; init; }

    public bool IsReady => State == DeviceState.Device;

    public bool IsWireless => Transport is DeviceTransport.WifiIp or DeviceTransport.WifiMdns;

    /// <summary>Nama untuk UI: model kalau ada, kalau tidak serial.</summary>
    public string DisplayName => string.IsNullOrEmpty(Model) ? Serial : Model;

    /// <summary>Untuk transport WifiIp: bagian host dari serial.</summary>
    public string? Host => Transport == DeviceTransport.WifiIp ? Serial[..Serial.LastIndexOf(':')] : null;

    public static DeviceTransport TransportFromSerial(string serial)
    {
        if (serial.Contains("._adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceTransport.WifiMdns;
        }

        if (serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceTransport.Emulator;
        }

        var colon = serial.LastIndexOf(':');
        if (colon > 0 && colon < serial.Length - 1 && serial[(colon + 1)..].All(char.IsAsciiDigit))
        {
            return DeviceTransport.WifiIp;
        }

        return DeviceTransport.Usb;
    }
}

/// <summary>Info tambahan dari <c>getprop</c> (§5.2).</summary>
public sealed record DeviceDetails(string? Manufacturer, string? Model, string? AndroidVersion)
{
    /// <summary>Mis. "Xiaomi 2407FPN8EG"; manufacturer tidak diulang kalau model sudah memuatnya.</summary>
    public string DisplayName
    {
        get
        {
            var model = Model ?? string.Empty;
            if (string.IsNullOrEmpty(Manufacturer) || model.StartsWith(Manufacturer, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }

            return $"{Manufacturer} {model}".Trim();
        }
    }
}
