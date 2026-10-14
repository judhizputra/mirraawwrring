using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Tests.Adb;

public sealed class AdbDevicesParserTests
{
    // Contoh nyata dari PROMPT.md §11.
    private const string Sample = """
        List of devices attached
        192.168.1.20:41235     device product:example model:2407FPN8EG device:example transport_id:3
        adb-FQYDJFRWIZGUZ989-8Usjcv._adb-tls-connect._tcp device product:example model:2407FPN8EG device:example transport_id:4
        R58N12ABCDE            unauthorized usb:1-1 transport_id:2
        emulator-5554          offline transport_id:1

        """;

    [Fact]
    public void Parse_ReadsAllDevicesWithStateAndTransport()
    {
        var devices = AdbDevicesParser.Parse(Sample);

        Assert.Equal(4, devices.Count);

        var wifi = devices[0];
        Assert.Equal("192.168.1.20:41235", wifi.Serial);
        Assert.Equal(DeviceState.Device, wifi.State);
        Assert.Equal(DeviceTransport.WifiIp, wifi.Transport);
        Assert.Equal("2407FPN8EG", wifi.Model);
        Assert.Equal("example", wifi.Product);
        Assert.Equal("example", wifi.DeviceCodename);
        Assert.Equal(3, wifi.TransportId);
        Assert.Equal("192.168.1.20", wifi.Host);
        Assert.True(wifi.IsReady);
        Assert.True(wifi.IsWireless);

        var mdns = devices[1];
        Assert.Equal(DeviceTransport.WifiMdns, mdns.Transport);
        Assert.Equal(4, mdns.TransportId);
        Assert.True(mdns.IsWireless);

        var usb = devices[2];
        Assert.Equal("R58N12ABCDE", usb.Serial);
        Assert.Equal(DeviceState.Unauthorized, usb.State);
        Assert.Equal(DeviceTransport.Usb, usb.Transport);
        Assert.Equal("1-1", usb.UsbPath);
        Assert.Null(usb.Model);
        Assert.Equal("R58N12ABCDE", usb.DisplayName);
        Assert.False(usb.IsReady);

        var emu = devices[3];
        Assert.Equal(DeviceState.Offline, emu.State);
        Assert.Equal(DeviceTransport.Emulator, emu.Transport);
    }

    [Fact]
    public void Parse_IgnoresDaemonLinesAndHeader()
    {
        const string output = """
            * daemon not running; starting now at tcp:5037
            * daemon started successfully
            List of devices attached
            R58N12ABCDE            device usb:1-1 product:p3s model:SM_G998B device:p3s transport_id:1

            """;

        var devices = AdbDevicesParser.Parse(output);

        var d = Assert.Single(devices);
        Assert.Equal("R58N12ABCDE", d.Serial);
        Assert.Equal("SM G998B", d.Model);
    }

    [Fact]
    public void Parse_HandlesNoPermissionsStateWithSpace()
    {
        const string output = """
            List of devices attached
            0123456789ABCDEF       no permissions usb:3-2 transport_id:5
            """;

        var d = Assert.Single(AdbDevicesParser.Parse(output));
        Assert.Equal(DeviceState.NoPermissions, d.State);
        Assert.Equal("no permissions", d.RawState);
        Assert.Equal("3-2", d.UsbPath);
    }

    [Theory]
    [InlineData("recovery", DeviceState.Recovery)]
    [InlineData("sideload", DeviceState.Sideload)]
    [InlineData("connecting", DeviceState.Unknown)]
    [InlineData("authorizing", DeviceState.Unknown)]
    public void Parse_MapsOtherStates(string raw, DeviceState expected)
    {
        var d = Assert.Single(AdbDevicesParser.Parse($"List of devices attached\nSERIAL1 {raw} transport_id:1\n"));
        Assert.Equal(expected, d.State);
        Assert.Equal(raw, d.RawState);
    }

    [Fact]
    public void Parse_EmptyOrErrorOutput_ReturnsEmpty()
    {
        Assert.Empty(AdbDevicesParser.Parse(string.Empty));
        Assert.Empty(AdbDevicesParser.Parse("List of devices attached\n\n"));
        Assert.Empty(AdbDevicesParser.Parse("error: cannot connect to daemon\n"));
    }

    [Fact]
    public void Parse_WindowsLineEndings()
    {
        var devices = AdbDevicesParser.Parse("List of devices attached\r\nABC123\tdevice\r\n\r\n");
        var d = Assert.Single(devices);
        Assert.Equal("ABC123", d.Serial);
        Assert.Equal(DeviceState.Device, d.State);
    }

    [Theory]
    [InlineData("192.168.1.20:5555", DeviceTransport.WifiIp)]
    [InlineData("adb-ABC._adb-tls-connect._tcp", DeviceTransport.WifiMdns)]
    [InlineData("emulator-5554", DeviceTransport.Emulator)]
    [InlineData("R58N12ABCDE", DeviceTransport.Usb)]
    [InlineData("ZY22ABCD:XX", DeviceTransport.Usb)]
    public void TransportFromSerial(string serial, DeviceTransport expected)
    {
        Assert.Equal(expected, Device.TransportFromSerial(serial));
    }
}
