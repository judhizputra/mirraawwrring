using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Usb;

namespace Mirraawwrring.Core.Tests.Usb;

public sealed class UsbProbeParserTests
{
    private static PnpEntry Entry(string id, string? name = null, string? cls = null, string[]? compat = null, string[]? hw = null) =>
        new(id, name, cls, compat ?? [], hw ?? []);

    [Fact]
    public void XiaomiWithAdbInterface_IsPhoneAndDebuggingOn()
    {
        var result = UsbProbeParser.Parse(
        [
            Entry(@"USB\VID_2717&PID_FF40\ABCDEF", "2407FPN8EG", "WPD", [@"USB\MS_COMP_MTP", @"USB\Class_06&SubClass_01&Prot_01"]),
            Entry(@"USB\VID_2717&PID_FF40&MI_01\7&1A2B&0&0001", "Android Composite ADB Interface", "AndroidUsbDeviceClass", [@"USB\Class_ff&SubClass_42&Prot_01", @"USB\Class_ff&SubClass_42", @"USB\Class_ff"]),
            Entry(@"USB\ROOT_HUB30\4&1&0", "USB Root Hub (USB 3.0)", "USB"),
            Entry(@"USB\VID_046D&PID_C52B\5&1", "USB Receiver", "USB"),
        ]);

        Assert.True(result.PhoneConnected);
        Assert.True(result.AdbInterfacePresent);
        Assert.True(result.MtpPresent);
        Assert.Equal("Xiaomi", result.Brand);
        Assert.Equal(2, result.Devices.Count);
        Assert.Equal("2717", result.Devices[0].VendorId);
        Assert.Equal("FF40", result.Devices[0].ProductId);
    }

    [Fact]
    public void PhoneWithoutAdbInterface_IsConnectedDebuggingOff()
    {
        var result = UsbProbeParser.Parse(
        [
            Entry(@"USB\VID_04E8&PID_6860\R58N12ABCDE", "SAMSUNG Mobile USB Device", "WPD", [@"USB\MS_COMP_MTP"]),
        ]);

        Assert.True(result.PhoneConnected);
        Assert.False(result.AdbInterfacePresent);
        Assert.Equal("Samsung", result.Brand);
    }

    [Fact]
    public void UnknownVendorWithAdbInterface_StillCountsAsPhone()
    {
        var result = UsbProbeParser.Parse(
        [
            Entry(@"USB\VID_ABCD&PID_0001&MI_01\X", "ADB Interface", null, [@"USB\Class_FF&SubClass_42&Prot_01"]),
        ]);

        Assert.True(result.PhoneConnected);
        Assert.True(result.AdbInterfacePresent);
        Assert.Null(result.Brand);
    }

    [Fact]
    public void OnlyMiceAndHubs_NoPhone()
    {
        var result = UsbProbeParser.Parse(
        [
            Entry(@"USB\ROOT_HUB30\4&1&0", "USB Root Hub", "USB"),
            Entry(@"USB\VID_046D&PID_C52B\5&1", "USB Receiver", "USB"),
            Entry(@"USB\VID_046D&PID_C52B&MI_00\6&1", "HID Keyboard", "HIDClass", [@"USB\Class_03&SubClass_01&Prot_01"]),
        ]);

        Assert.False(result.PhoneConnected);
        Assert.False(result.AdbInterfacePresent);
    }

    [Fact]
    public void NonUsbEntries_Ignored()
    {
        var result = UsbProbeParser.Parse([Entry(@"PCI\VEN_8086&DEV_1234\3&1", "PCI", "System")]);
        Assert.Empty(result.Devices);
    }

    [Theory]
    [InlineData("2717", "Xiaomi")]
    [InlineData("04e8", "Samsung")]
    [InlineData("18D1", "Google")]
    [InlineData("22D9", "OPPO/realme/OnePlus")]
    [InlineData("2D95", "vivo")]
    [InlineData("12D1", "Huawei")]
    [InlineData("22B8", "Motorola")]
    [InlineData("FFFF", null)]
    public void VendorTable(string vid, string? brand)
    {
        Assert.Equal(brand, UsbVendorTable.BrandFor(vid));
    }
}

public sealed class PhoneStatusEvaluatorTests
{
    private static UsbProbeResult Usb(bool phone, bool adbInterface) =>
        new(phone
            ? [new UsbDeviceInfo(@"USB\VID_2717&PID_FF40\X", "Xiaomi", "2717", "FF40", "Xiaomi", adbInterface, true)]
            : []);

    [Fact]
    public void NotConnected() => Assert.Equal(PhoneUsbStatus.NotConnected, PhoneStatusEvaluator.Evaluate(Usb(false, false), []));

    [Fact]
    public void ConnectedDebuggingOff() => Assert.Equal(PhoneUsbStatus.ConnectedDebuggingOff, PhoneStatusEvaluator.Evaluate(Usb(true, false), []));

    [Fact]
    public void DebuggingOnButAdbBlind() => Assert.Equal(PhoneUsbStatus.DebuggingOnButAdbBlind, PhoneStatusEvaluator.Evaluate(Usb(true, true), []));

    [Fact]
    public void Unauthorized() =>
        Assert.Equal(PhoneUsbStatus.Unauthorized, PhoneStatusEvaluator.Evaluate(Usb(true, true), [TestDevices.Usb(state: DeviceState.Unauthorized)]));

    [Fact]
    public void Offline_TreatedAsNotReadyYet() =>
        Assert.Equal(PhoneUsbStatus.Unauthorized, PhoneStatusEvaluator.Evaluate(Usb(true, true), [TestDevices.Usb(state: DeviceState.Offline)]));

    [Fact]
    public void Ready() =>
        Assert.Equal(PhoneUsbStatus.Ready, PhoneStatusEvaluator.Evaluate(Usb(true, true), [TestDevices.Usb()]));

    [Fact]
    public void WifiOnlyDevice_DoesNotCountAsUsbReady() =>
        Assert.Equal(PhoneUsbStatus.NotConnected, PhoneStatusEvaluator.Evaluate(Usb(false, false), [TestDevices.Wifi()]));
}
