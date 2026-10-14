using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Tests.Adb;

public sealed class AdbPairResultParserTests
{
    [Fact]
    public void Success_ExtractsAddressAndGuid()
    {
        var r = AdbPairResultParser.Parse("Successfully paired to 192.168.1.20:37099 [guid=adb-FQYDJFRWIZGUZ989-8Usjcv]\n");

        Assert.True(r.Success);
        Assert.Equal("192.168.1.20:37099", r.Address);
        Assert.Equal("adb-FQYDJFRWIZGUZ989-8Usjcv", r.Guid);
    }

    [Theory]
    [InlineData("Failed: Unable to start pairing client.")]
    [InlineData("Failed: Wrong password or connection was dropped.")]
    [InlineData("error: no devices/emulators found")]
    public void Failure_WhenNoSuccessPhrase(string output)
    {
        var r = AdbPairResultParser.Parse(output);

        Assert.False(r.Success);
        Assert.Null(r.Address);
        Assert.Equal(output, r.Message);
    }

    [Fact]
    public void EmptyOutput_IsFailureWithMessage()
    {
        var r = AdbPairResultParser.Parse("  ");
        Assert.False(r.Success);
        Assert.NotEmpty(r.Message);
    }
}

public sealed class AdbConnectResultParserTests
{
    [Fact]
    public void Connected()
    {
        var r = AdbConnectResultParser.Parse("connected to 192.168.1.20:41235");
        Assert.True(r.Success);
        Assert.False(r.AlreadyConnected);
        Assert.Equal("192.168.1.20:41235", r.Address);
    }

    [Fact]
    public void AlreadyConnected()
    {
        var r = AdbConnectResultParser.Parse("already connected to 192.168.1.20:41235");
        Assert.True(r.Success);
        Assert.True(r.AlreadyConnected);
        Assert.Equal("192.168.1.20:41235", r.Address);
    }

    [Theory]
    [InlineData("failed to connect to 192.168.1.20:41235")]
    [InlineData("cannot connect to 192.168.1.20:41235: No connection could be made because the target machine actively refused it. (10061)")]
    [InlineData("failed to resolve host 'hp.local': Name or service not known")]
    public void Failures(string output)
    {
        var r = AdbConnectResultParser.Parse(output);
        Assert.False(r.Success);
        Assert.Null(r.Address);
        Assert.Equal(output, r.Message);
    }

    [Fact]
    public void SuccessAfterDaemonLines()
    {
        var r = AdbConnectResultParser.Parse("* daemon not running; starting now at tcp:5037\n* daemon started successfully\nconnected to 192.168.1.20:41235\n");
        Assert.True(r.Success);
        Assert.Equal("192.168.1.20:41235", r.Address);
    }
}

public sealed class AdbServerOutputParserTests
{
    [Fact]
    public void VersionMismatchAndPortInUse_IsFailure()
    {
        const string output = """
            adb server version (41) doesn't match this client (39); killing...
            error: could not install *smartsocket* listener: Address already in use
            """;

        var r = AdbServerOutputParser.Parse(output, exitCode: 1);

        Assert.False(r.Success);
        Assert.True(r.VersionMismatch);
        Assert.True(r.PortInUse);
    }

    [Fact]
    public void VersionMismatchButRestartedOk_IsSuccessWithWarning()
    {
        var r = AdbServerOutputParser.Parse("adb server version (41) doesn't match this client (39); killing...\n* daemon started successfully", exitCode: 0);

        Assert.True(r.Success);
        Assert.True(r.VersionMismatch);
        Assert.False(r.PortInUse);
    }

    [Fact]
    public void NormalStart()
    {
        var r = AdbServerOutputParser.Parse("* daemon not running; starting now at tcp:5037\n* daemon started successfully", exitCode: 0);
        Assert.True(r.Success);
        Assert.False(r.VersionMismatch);
        Assert.False(r.PortInUse);
    }

    [Fact]
    public void NonZeroExit_IsFailure()
    {
        var r = AdbServerOutputParser.Parse("", exitCode: 1);
        Assert.False(r.Success);
    }
}

public sealed class MdnsServicesParserTests
{
    [Fact]
    public void Parse_TabSeparated()
    {
        const string output = "List of discovered mdns services\n" +
                              "adb-FQYDJFRWIZGUZ989-8Usjcv\t_adb-tls-connect._tcp\t192.168.1.20:41235\n" +
                              "adb-FQYDJFRWIZGUZ989-8Usjcv\t_adb-tls-pairing._tcp\t192.168.1.20:37099\n";

        var services = MdnsServicesParser.Parse(output);

        Assert.Equal(2, services.Count);
        Assert.Equal("adb-FQYDJFRWIZGUZ989-8Usjcv", services[0].Name);
        Assert.True(services[0].IsConnectService);
        Assert.Equal("192.168.1.20", services[0].Host);
        Assert.Equal(41235, services[0].Port);
        Assert.Equal("192.168.1.20:41235", services[0].Address);
        Assert.True(services[1].IsPairingService);
        Assert.Equal(37099, services[1].Port);
    }

    [Fact]
    public void Parse_SkipsMalformedLines()
    {
        var services = MdnsServicesParser.Parse("List of discovered mdns services\nbroken line\nname\t_adb._tcp\tnoport\n");
        Assert.Empty(services);
    }
}

public sealed class WifiIpParserTests
{
    [Fact]
    public void ParseInetAddr_FromWlan0()
    {
        const string output = """
            24: wlan0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP group default qlen 3000
                inet 192.168.1.20/24 brd 192.168.1.255 scope global wlan0
                   valid_lft forever preferred_lft forever
            """;
        Assert.Equal("192.168.1.20", WifiIpParser.ParseInetAddr(output));
    }

    [Fact]
    public void ParseRouteSource()
    {
        const string output = "192.168.1.0/24 dev wlan0 proto kernel scope link src 192.168.1.20\n";
        Assert.Equal("192.168.1.20", WifiIpParser.ParseRouteSource(output));
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        Assert.Null(WifiIpParser.ParseInetAddr("Device \"wlan0\" does not exist."));
        Assert.Null(WifiIpParser.ParseRouteSource(string.Empty));
    }
}
