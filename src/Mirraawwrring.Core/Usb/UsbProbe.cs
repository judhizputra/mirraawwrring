using System.Text.RegularExpressions;

namespace Mirraawwrring.Core.Usb;

/// <summary>Satu entri Win32_PnPEntity (hanya kolom yang kita pakai). Dipisah dari WMI agar parser bisa diuji.</summary>
public sealed record PnpEntry(string DeviceId, string? Name, string? PnpClass, IReadOnlyList<string> CompatibleIds, IReadOnlyList<string> HardwareIds);

public sealed record UsbDeviceInfo(string DeviceId, string? Name, string? VendorId, string? ProductId, string? Brand, bool IsAdbInterface, bool IsMtpOrPtp)
{
    public bool LooksLikePhone => Brand is not null || IsAdbInterface || IsMtpOrPtp;
}

/// <summary>Hasil pemindaian USB tanpa adb (§5.3).</summary>
public sealed record UsbProbeResult(IReadOnlyList<UsbDeviceInfo> Devices)
{
    public static readonly UsbProbeResult Empty = new([]);

    /// <summary>Ada perangkat USB yang dikenali sebagai HP Android (VID dikenal, antarmuka ADB, atau MTP/PTP).</summary>
    public bool PhoneConnected => Devices.Any(d => d.LooksLikePhone);

    /// <summary>Ada antarmuka ADB (<c>USB\Class_FF&amp;SubClass_42&amp;Prot_01</c>) = USB debugging aktif.</summary>
    public bool AdbInterfacePresent => Devices.Any(d => d.IsAdbInterface);

    public bool MtpPresent => Devices.Any(d => d.IsMtpOrPtp);

    /// <summary>Merek HP pertama yang dikenali, mis. "Xiaomi".</summary>
    public string? Brand => Devices.Select(d => d.Brand).FirstOrDefault(b => b is not null);
}

/// <summary>Parser murni: entri PnP → info HP.</summary>
public static partial class UsbProbeParser
{
    [GeneratedRegex(@"VID_(?<vid>[0-9A-Fa-f]{4})(?:&PID_(?<pid>[0-9A-Fa-f]{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();

    /// <summary>CompatibleID antarmuka ADB: kelas vendor-specific FF, subclass 42, protokol 01.</summary>
    public const string AdbCompatibleId = @"USB\Class_FF&SubClass_42&Prot_01";

    /// <summary>CompatibleID MTP/PTP (Still Image Capture, PTP) dan kelas WPD.</summary>
    private static readonly string[] MtpCompatibleIds = [@"USB\Class_06&SubClass_01&Prot_01", @"USB\MS_COMP_MTP", @"USB\Class_06"];

    public static UsbProbeResult Parse(IEnumerable<PnpEntry> entries)
    {
        var devices = new List<UsbDeviceInfo>();
        foreach (var e in entries)
        {
            if (!e.DeviceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var m = VidPidRegex().Match(e.DeviceId);
            var vid = m.Success ? m.Groups["vid"].Value.ToUpperInvariant() : null;
            var pid = m.Success && m.Groups["pid"].Success ? m.Groups["pid"].Value.ToUpperInvariant() : null;
            var isAdb = e.CompatibleIds.Any(c => c.Equals(AdbCompatibleId, StringComparison.OrdinalIgnoreCase))
                        || e.HardwareIds.Any(h => h.Contains("ADB", StringComparison.OrdinalIgnoreCase) && h.Contains("USB", StringComparison.OrdinalIgnoreCase));
            var isMtp = e.CompatibleIds.Any(c => MtpCompatibleIds.Any(k => c.Equals(k, StringComparison.OrdinalIgnoreCase)))
                        || string.Equals(e.PnpClass, "WPD", StringComparison.OrdinalIgnoreCase)
                        || (e.Name?.Contains("MTP", StringComparison.OrdinalIgnoreCase) ?? false);
            // Hub USB dan perangkat tanpa VID dilewati.
            if (vid is null || string.Equals(e.PnpClass, "USB", StringComparison.OrdinalIgnoreCase) && !isAdb && !isMtp && UsbVendorTable.BrandFor(vid) is null)
            {
                continue;
            }

            devices.Add(new UsbDeviceInfo(e.DeviceId, e.Name, vid, pid, UsbVendorTable.BrandFor(vid), isAdb, isMtp));
        }

        return new UsbProbeResult(devices);
    }
}

/// <summary>Sumber data USB; implementasi nyata memakai WMI, di test diganti fake.</summary>
public interface IUsbProbe
{
    Task<UsbProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}
