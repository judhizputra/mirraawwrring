using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests.Devices;

public sealed class InjectionProbeTests
{
    private const string BlockedOutput = "java.lang.SecurityException: Injecting input events requires the caller (or the source of the instrumentation, if any) to have the INJECT_EVENTS permission.";

    [Theory]
    [InlineData(false, 0, "", InjectionStatus.Allowed)]
    [InlineData(false, 255, BlockedOutput, InjectionStatus.Blocked)]
    [InlineData(false, 0, BlockedOutput, InjectionStatus.Blocked)]
    [InlineData(false, 1, "error: device offline", InjectionStatus.Error)]
    [InlineData(true, -1, "", InjectionStatus.Error)]
    public void Classify(bool timedOut, int exitCode, string output, InjectionStatus expected)
    {
        Assert.Equal(expected, InjectionProbe.Classify(timedOut, exitCode, output));
    }

    [Fact]
    public async Task Probe_UsesCache_RecheckIgnoresIt()
    {
        var adb = new FakeAdbClient { ShellResponse = (_, _) => ProcessResult.Fail(255, "", BlockedOutput) };
        var probe = new InjectionProbe(adb, NullLogger<InjectionProbe>.Instance);

        Assert.Equal(InjectionStatus.Unknown, probe.GetCached("SER"));
        Assert.Equal(InjectionStatus.Blocked, await probe.ProbeAsync("SER"));
        Assert.Equal(InjectionStatus.Blocked, await probe.ProbeAsync("SER"));
        Assert.Single(adb.Calls);
        Assert.Equal("shell SER input keyevent 0", adb.Calls[0]);

        adb.ShellResponse = (_, _) => ProcessResult.Ok();
        Assert.Equal(InjectionStatus.Allowed, await probe.RecheckAsync("SER"));
        Assert.Equal(2, adb.Calls.Count);
        Assert.Equal(InjectionStatus.Allowed, probe.GetCached("SER"));
    }

    [Fact]
    public async Task Error_IsNotCachedAsFinal()
    {
        var adb = new FakeAdbClient { ShellResponse = (_, _) => ProcessResult.Timeout() };
        var probe = new InjectionProbe(adb, NullLogger<InjectionProbe>.Instance);

        Assert.Equal(InjectionStatus.Error, await probe.ProbeAsync("SER"));
        adb.ShellResponse = (_, _) => ProcessResult.Ok();
        Assert.Equal(InjectionStatus.Allowed, await probe.ProbeAsync("SER"));
    }
}
