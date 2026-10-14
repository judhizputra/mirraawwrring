using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Usb;

/// <summary>Status gabungan USB + adb (§5.3).</summary>
public enum PhoneUsbStatus
{
    /// <summary>Tidak ada HP Android yang tercolok USB.</summary>
    NotConnected,

    /// <summary>HP tercolok (MTP/VID dikenal) tetapi antarmuka ADB tidak ada: USB debugging mati.</summary>
    ConnectedDebuggingOff,

    /// <summary>Antarmuka ADB ada tetapi adb tidak melihat perangkat: biasanya driver USB.</summary>
    DebuggingOnButAdbBlind,

    /// <summary>adb melihat perangkat dengan state unauthorized: popup izin belum disetujui.</summary>
    Unauthorized,

    /// <summary>adb melihat perangkat USB dengan state device.</summary>
    Ready,
}

/// <summary>Fungsi murni penggabung hasil UsbProbe dan daftar adb.</summary>
public static class PhoneStatusEvaluator
{
    public static PhoneUsbStatus Evaluate(UsbProbeResult usb, IReadOnlyList<Device> adbDevices)
    {
        var usbAdbDevices = adbDevices.Where(d => d.Transport == DeviceTransport.Usb).ToList();
        if (usbAdbDevices.Any(d => d.State == DeviceState.Device))
        {
            return PhoneUsbStatus.Ready;
        }

        if (usbAdbDevices.Any(d => d.State == DeviceState.Unauthorized))
        {
            return PhoneUsbStatus.Unauthorized;
        }

        if (usbAdbDevices.Count > 0)
        {
            // offline / no permissions: adb melihat tetapi belum bisa dipakai; perlakukan seperti belum diizinkan.
            return PhoneUsbStatus.Unauthorized;
        }

        if (usb.AdbInterfacePresent)
        {
            return PhoneUsbStatus.DebuggingOnButAdbBlind;
        }

        return usb.PhoneConnected ? PhoneUsbStatus.ConnectedDebuggingOff : PhoneUsbStatus.NotConnected;
    }
}
