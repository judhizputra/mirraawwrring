using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.Core.Tests.Wireless;

public sealed class QrPairingTests
{
    [Fact]
    public void Create_ProducesAndroidStudioStyleQr()
    {
        var session = QrPairing.Create();

        Assert.StartsWith("mirraawwrring-", session.ServiceName);
        Assert.Equal(QrPairing.ServicePrefix.Length + 8, session.ServiceName.Length);
        Assert.Equal(12, session.Password.Length);
        Assert.Equal($"WIFI:T:ADB;S:{session.ServiceName};P:{session.Password};;", session.QrContent);
        Assert.DoesNotContain(';', session.Password);
        Assert.DoesNotContain(':', session.Password);
    }

    [Fact]
    public void Create_IsRandom()
    {
        var a = QrPairing.Create();
        var b = QrPairing.Create();
        Assert.NotEqual(a.ServiceName, b.ServiceName);
        Assert.NotEqual(a.Password, b.Password);
    }

    [Fact]
    public void FindPairingService_MatchesNameAndTypeOnly()
    {
        var services = MdnsServicesParser.Parse(
            "List of discovered mdns services\n" +
            "adb-FQYDJFRWIZGUZ989-8Usjcv\t_adb-tls-connect._tcp\t192.168.1.4:44769\n" +
            "mirraawwrring-AbCd2345\t_adb-tls-connect._tcp\t192.168.1.4:44769\n" +
            "MIRRAAWWRRING-abcd2345\t_adb-tls-pairing._tcp\t192.168.1.4:40123\n");

        var found = QrPairing.FindPairingService(services, "mirraawwrring-AbCd2345");

        Assert.NotNull(found);
        Assert.Equal("192.168.1.4:40123", found.Address);
        Assert.Null(QrPairing.FindPairingService(services, "mirraawwrring-lain"));
    }

    [Fact]
    public async Task WaitForPairingService_PollsUntilFound()
    {
        var adb = new FakeAdbClient();
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new WirelessService(adb, new FakeSettingsStore(), NullLogger<WirelessService>.Instance, time);
        var pairing = new MdnsService("mirraawwrring-x", MdnsService.PairingType, "192.168.1.4", 40123);
        var task = service.WaitForPairingServiceAsync("mirraawwrring-x", TimeSpan.FromSeconds(30));

        // Belum ada layanan pada poll pertama; muncul setelah itu.
        adb.MdnsResponse = [pairing];
        time.Advance(TimeSpan.FromSeconds(1));
        var found = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(found);
        Assert.Equal("192.168.1.4:40123", found.Address);
        Assert.Contains("mdns", adb.Calls);
    }

    [Fact]
    public async Task WaitForPairingService_TimesOut()
    {
        var adb = new FakeAdbClient();
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new WirelessService(adb, new FakeSettingsStore(), NullLogger<WirelessService>.Instance, time);

        var task = service.WaitForPairingServiceAsync("mirraawwrring-x", TimeSpan.FromSeconds(2));
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(20);
            time.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Null(await task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task PairWithPassword_SkipsSixDigitRule()
    {
        var adb = new FakeAdbClient { PairResponse = new PairResult(true, "192.168.1.4:40123", "adb-FAKE", "Successfully paired to 192.168.1.4:40123 [guid=adb-FAKE]") };
        adb.Enqueue(TestDevices.Mdns("adb-FAKE"));
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new WirelessService(adb, new FakeSettingsStore(), NullLogger<WirelessService>.Instance, time);

        var task = service.PairWithPasswordAsync("192.168.1.4:40123", "AbCd2345EfGh");
        time.Advance(TimeSpan.FromSeconds(1));
        var outcome = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(outcome.Paired);
        Assert.Equal("pair 192.168.1.4:40123 AbCd2345EfGh", adb.Calls[0]);
        Assert.Equal("adb-FAKE._adb-tls-connect._tcp", outcome.ConnectedSerial);
    }
}
