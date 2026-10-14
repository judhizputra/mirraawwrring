using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.Core.Tests.Wireless;

public sealed class WirelessServiceTests
{
    private readonly FakeAdbClient _adb = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly WirelessService _service;

    public WirelessServiceTests()
    {
        // Delay 1 detik di service dijalankan lewat TimeProvider nyata, tetapi test memakai antrean devices sehingga koneksi
        // otomatis ditemukan pada poll pertama (1 detik). Kasus timeout memakai FakeTimeProvider di bawah.
        _service = new WirelessService(_adb, _settings, NullLogger<WirelessService>.Instance);
    }

    [Fact]
    public async Task Pair_InvalidAddress_DoesNotCallAdb()
    {
        var outcome = await _service.PairAsync("192.168.1.20", "123456");

        Assert.False(outcome.Paired);
        Assert.Empty(_adb.Calls);
    }

    [Fact]
    public async Task Pair_InvalidCode_DoesNotCallAdb()
    {
        var outcome = await _service.PairAsync("192.168.1.20:37099", "12345");

        Assert.False(outcome.Paired);
        Assert.Empty(_adb.Calls);
    }

    [Fact]
    public async Task Pair_Failure_ReturnsAdbMessage()
    {
        var outcome = await _service.PairAsync("192.168.1.20:37099", "123456");

        Assert.False(outcome.Paired);
        Assert.Contains("Unable to start pairing client", outcome.Message);
        Assert.Equal(["pair 192.168.1.20:37099 123456"], _adb.Calls);
    }

    [Fact]
    public async Task Pair_Success_AutoConnectsViaMdns()
    {
        _adb.PairResponse = new PairResult(true, "192.168.1.20:37099", "adb-X", "Successfully paired to 192.168.1.20:37099 [guid=adb-X]");
        _adb.Enqueue(TestDevices.Mdns());

        var outcome = await _service.PairAsync("192.168.1.20:37099", "482 113");

        Assert.True(outcome.Paired);
        Assert.False(outcome.NeedsConnectPort);
        Assert.Equal(TestDevices.Mdns().Serial, outcome.ConnectedSerial);
        Assert.Equal("pair 192.168.1.20:37099 482113", _adb.Calls[0]);
    }

    [Fact]
    public async Task Pair_Success_ButOnlyUnrelatedDevices_NeedsPort()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = new WirelessService(_adb, _settings, NullLogger<WirelessService>.Instance, time);
        _adb.PairResponse = new PairResult(true, "192.168.1.20:37099", "adb-X", "Successfully paired");
        _adb.Enqueue(TestDevices.Usb(), TestDevices.Wifi("10.0.0.9:5555"));

        var task = service.PairAsync("192.168.1.20:37099", "123456");
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(10);
            time.Advance(TimeSpan.FromSeconds(1));
        }

        var outcome = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(outcome.Paired);
        Assert.True(outcome.NeedsConnectPort);
        Assert.Null(outcome.ConnectedSerial);
        Assert.True(_adb.GetDevicesCalls >= 1);
    }

    [Fact]
    public async Task Connect_NormalizesPortAndRemembersHistory()
    {
        var result = await _service.ConnectAsync("192.168.1.20");

        Assert.True(result.Success);
        Assert.Equal(["connect 192.168.1.20:5555"], _adb.Calls);
        Assert.Equal(["192.168.1.20:5555"], _service.ConnectionHistory);
    }

    [Fact]
    public async Task Connect_Failure_NotRemembered()
    {
        _adb.ConnectResponse = addr => new ConnectResult(false, false, null, $"cannot connect to {addr}: refused");

        var result = await _service.ConnectAsync("192.168.1.20:41235");

        Assert.False(result.Success);
        Assert.Empty(_service.ConnectionHistory);
    }

    [Fact]
    public async Task Connect_InvalidAddress()
    {
        var result = await _service.ConnectAsync("??");
        Assert.False(result.Success);
        Assert.Empty(_adb.Calls);
    }

    [Fact]
    public void History_MovesToFrontAndCaps()
    {
        for (var i = 0; i < 12; i++)
        {
            _service.RememberAddress($"10.0.0.{i}:5555");
        }

        _service.RememberAddress("10.0.0.3:5555");

        Assert.Equal(WirelessService.MaxHistory, _service.ConnectionHistory.Count);
        Assert.Equal("10.0.0.3:5555", _service.ConnectionHistory[0]);
        Assert.Equal("10.0.0.11:5555", _service.ConnectionHistory[1]);

        _service.ForgetAddress("10.0.0.3:5555");
        Assert.DoesNotContain("10.0.0.3:5555", _service.ConnectionHistory);
    }

    [Fact]
    public async Task EnableWifiFromUsb_HappyPath()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = new WirelessService(_adb, _settings, NullLogger<WirelessService>.Instance, time);

        var task = service.EnableWifiFromUsbAsync("R58N12ABCDE");
        await Task.Delay(20);
        time.Advance(TimeSpan.FromSeconds(3));
        var outcome = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(outcome.Success);
        Assert.Equal("192.168.1.20:5555", outcome.Address);
        Assert.Equal(["wifi-ip R58N12ABCDE", "tcpip R58N12ABCDE 5555", "connect 192.168.1.20:5555"], _adb.Calls);
        Assert.Contains("192.168.1.20:5555", service.ConnectionHistory);
    }

    [Fact]
    public async Task EnableWifiFromUsb_NoIp()
    {
        _adb.WifiIp = null;

        var outcome = await _service.EnableWifiFromUsbAsync("R58N12ABCDE");

        Assert.False(outcome.Success);
        Assert.Equal(["wifi-ip R58N12ABCDE"], _adb.Calls);
    }

    [Fact]
    public void IsWirelessMatch()
    {
        Assert.True(WirelessService.IsWirelessMatch(TestDevices.Mdns(), "192.168.1.20"));
        Assert.True(WirelessService.IsWirelessMatch(TestDevices.Wifi("192.168.1.20:41235"), "192.168.1.20"));
        Assert.False(WirelessService.IsWirelessMatch(TestDevices.Wifi("10.0.0.9:5555"), "192.168.1.20"));
        Assert.False(WirelessService.IsWirelessMatch(TestDevices.Usb(), "192.168.1.20"));
    }
}

public sealed class MdnsWatcherTests
{
    [Fact]
    public async Task Refresh_ReportsOnlyConnectServices_AndOnlyOnChange()
    {
        var adb = new FakeAdbClient
        {
            MdnsResponse =
            [
                new MdnsService("adb-X", MdnsService.ConnectType, "192.168.1.20", 41235),
                new MdnsService("adb-X", MdnsService.PairingType, "192.168.1.20", 37099),
            ],
        };
        await using var watcher = new MdnsWatcher(adb, NullLogger<MdnsWatcher>.Instance);
        var changes = 0;
        watcher.ServicesChanged += _ => changes++;

        await watcher.RefreshAsync();
        await watcher.RefreshAsync();

        Assert.Equal(1, changes);
        var s = Assert.Single(watcher.Services);
        Assert.Equal("192.168.1.20:41235", s.Address);

        adb.MdnsResponse = [];
        await watcher.RefreshAsync();
        Assert.Equal(2, changes);
        Assert.Empty(watcher.Services);
    }

    [Fact]
    public async Task Refresh_FailureKeepsLastList()
    {
        var adb = new FakeAdbClient { MdnsResponse = [new MdnsService("adb-X", MdnsService.ConnectType, "192.168.1.20", 41235)] };
        await using var watcher = new MdnsWatcher(adb, NullLogger<MdnsWatcher>.Instance);
        await watcher.RefreshAsync();

        adb.MdnsFailure = new InvalidOperationException("adb mati");
        await watcher.RefreshAsync();

        Assert.Single(watcher.Services);
    }
}
