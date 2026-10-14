using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Tests.Adb;

public sealed class AdbClientTests
{
    private const string AdbPath = @"C:\tools\scrcpy\adb.exe";
    private readonly FakeProcessRunner _runner = new();
    private readonly AdbClient _adb;

    public AdbClientTests()
    {
        var registry = new ToolRegistry();
        registry.Update(new ToolLocation { AdbPath = AdbPath, ScrcpyPath = @"C:\tools\scrcpy\scrcpy.exe" });
        _adb = new AdbClient(_runner, registry, NullLogger<AdbClient>.Instance);
    }

    [Fact]
    public async Task GetDevices_RunsDevicesDashLAndParses()
    {
        _runner.When(["devices", "-l"], ProcessResult.Ok("List of devices attached\nR58N12ABCDE device model:SM_G998B transport_id:2\n"));

        var devices = await _adb.GetDevicesAsync();

        Assert.Equal(AdbPath, _runner.Last.FileName);
        Assert.Equal(["devices", "-l"], _runner.Last.Arguments);
        Assert.Equal(ProcessRequest.DefaultTimeout, _runner.Last.Timeout);
        var d = Assert.Single(devices);
        Assert.Equal("SM G998B", d.Model);
    }

    [Fact]
    public async Task Shell_EscapesArgumentsIntoSingleShellCommand()
    {
        await _adb.ShellAsync("SER", ["mv", "/sdcard/a b.txt", "/sdcard/c.txt"]);

        Assert.Equal(["-s", "SER", "shell", "mv '/sdcard/a b.txt' /sdcard/c.txt"], _runner.Last.Arguments);
    }

    [Fact]
    public async Task GetDeviceDetails_UsesGetprop()
    {
        _runner
            .When(r => r.Arguments[^1].Contains("ro.product.manufacturer"), _ => ProcessResult.Ok("Xiaomi\n"))
            .When(r => r.Arguments[^1].Contains("ro.product.model"), _ => ProcessResult.Ok("2407FPN8EG\r\n"))
            .When(r => r.Arguments[^1].Contains("ro.build.version.release"), _ => ProcessResult.Ok("16\n"));

        var details = await _adb.GetDeviceDetailsAsync("SER");

        Assert.Equal("Xiaomi", details.Manufacturer);
        Assert.Equal("2407FPN8EG", details.Model);
        Assert.Equal("16", details.AndroidVersion);
        Assert.Equal("Xiaomi 2407FPN8EG", details.DisplayName);
        Assert.All(_runner.Requests, r => Assert.Equal("shell", r.Arguments[2]));
    }

    [Fact]
    public async Task Pair_UsesNetworkTimeoutAndParsesResult()
    {
        _runner.When(["pair"], ProcessResult.Ok("Successfully paired to 192.168.1.20:37099 [guid=adb-X]"));

        var result = await _adb.PairAsync(" 192.168.1.20:37099 ", "482 113");

        Assert.Equal(["pair", "192.168.1.20:37099", "482 113"], _runner.Last.Arguments);
        Assert.Equal(AdbClient.NetworkTimeout, _runner.Last.Timeout);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Pair_Timeout_IsFailure()
    {
        _runner.When(["pair"], ProcessResult.Timeout());

        var result = await _adb.PairAsync("192.168.1.20:37099", "123456");

        Assert.False(result.Success);
        Assert.Contains("timeout", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Connect_ParsesStderrToo()
    {
        _runner.When(["connect"], ProcessResult.Fail(1, "", "cannot connect to 192.168.1.20:41235: refused"));

        var result = await _adb.ConnectAsync("192.168.1.20:41235");

        Assert.False(result.Success);
        Assert.Contains("refused", result.Message);
    }

    [Fact]
    public async Task TcpIp_DefaultPort5555()
    {
        await _adb.TcpIpAsync("SER");
        Assert.Equal(["-s", "SER", "tcpip", "5555"], _runner.Last.Arguments);
    }

    [Fact]
    public async Task GetWifiIp_FallsBackToRoute()
    {
        _runner
            .When(r => r.Arguments[^1].StartsWith("ip -f inet", StringComparison.Ordinal), _ => ProcessResult.Fail(1, "", "Device \"wlan0\" does not exist."))
            .When(r => r.Arguments[^1] == "ip route", _ => ProcessResult.Ok("192.168.1.0/24 dev wlan1 proto kernel scope link src 192.168.1.33\n"));

        var ip = await _adb.GetWifiIpAsync("SER");

        Assert.Equal("192.168.1.33", ip);
        Assert.Equal(2, _runner.Requests.Count);
    }

    [Fact]
    public async Task StartServer_DetectsVersionConflict()
    {
        _runner.When(["start-server"], ProcessResult.Fail(1, "", "adb server version (41) doesn't match this client (39); killing...\nerror: could not install *smartsocket* listener: Address already in use"));

        var result = await _adb.StartServerAsync();

        Assert.False(result.Success);
        Assert.True(result.VersionMismatch);
        Assert.True(result.PortInUse);
    }

    [Fact]
    public async Task MdnsServices()
    {
        _runner.When(["mdns", "services"], ProcessResult.Ok("List of discovered mdns services\nadb-X\t_adb-tls-connect._tcp\t192.168.1.20:41235\n"));

        var services = await _adb.GetMdnsServicesAsync();

        var s = Assert.Single(services);
        Assert.Equal("192.168.1.20:41235", s.Address);
    }

    [Fact]
    public async Task NoAdbRegistered_ThrowsToolNotFound()
    {
        var adb = new AdbClient(_runner, new ToolRegistry(), NullLogger<AdbClient>.Instance);

        var ex = await Assert.ThrowsAsync<ToolNotFoundException>(() => adb.GetDevicesAsync());
        Assert.Equal("adb.exe", ex.ToolName);
    }

    [Fact]
    public async Task Run_BinaryStdoutFlagIsForwarded()
    {
        await _adb.RunAsync(["-s", "SER", "exec-out", "screencap", "-p"], binaryStdout: true);
        Assert.True(_runner.Last.BinaryStdout);
    }
}
