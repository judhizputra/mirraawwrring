using Mirraawwrring.Core.Scrcpy;

namespace Mirraawwrring.Core.Tests.Scrcpy;

public sealed class ScrcpyLogClassifierTests
{
    // Log nyata dari HP pemilik (PROMPT.md §11).
    private const string BlockedSession = """
        scrcpy 4.1 <https://github.com/Genymobile/scrcpy>
        INFO: ADB device found:
        INFO:     --> (tcpip)  adb-FQYDJFRWIZGUZ989-8Usjcv._adb-tls-connect._tcp            device  2407FPN8EG
        [server] INFO: Device: [Xiaomi] Xiaomi 2407FPN8EG (Android 16)
        [server] ERROR: Injecting input events requires the caller (or the source of the instrumentation, if any) to have the INJECT_EVENTS permission.
        [server] ERROR: Make sure you have enabled "USB debugging (Security Settings)" and then rebooted your device.
        INFO: Renderer: direct3d11
        INFO: Texture: 1220x2712
        """;

    [Fact]
    public void BlockedSession_ClassifiesEveryLine()
    {
        var lines = BlockedSession.Split('\n').Select(ScrcpyLogClassifier.Classify).ToList();

        Assert.Equal(ScrcpyLogKind.Version, lines[0].Kind);
        Assert.Equal("4.1", lines[0].Detail);
        Assert.Equal(ScrcpyLogLevel.Other, lines[0].Level);

        Assert.Equal(ScrcpyLogKind.DeviceFound, lines[1].Kind);
        Assert.Equal(ScrcpyLogKind.None, lines[2].Kind);
        Assert.Equal(ScrcpyLogLevel.Info, lines[2].Level);

        Assert.Equal(ScrcpyLogKind.DeviceInfo, lines[3].Kind);
        Assert.True(lines[3].FromServer);
        Assert.Equal("[Xiaomi] Xiaomi 2407FPN8EG (Android 16)", lines[3].Detail);

        Assert.Equal(ScrcpyLogKind.InjectionBlocked, lines[4].Kind);
        Assert.Equal(ScrcpyLogLevel.Error, lines[4].Level);
        Assert.True(lines[4].FromServer);
        Assert.True(lines[4].IsProblem);
        Assert.Equal(ScrcpyLogKind.InjectionBlocked, lines[5].Kind);

        Assert.Equal(ScrcpyLogKind.Rendering, lines[6].Kind);
        Assert.Equal("direct3d11", lines[6].Detail);
        Assert.Equal(ScrcpyLogKind.Rendering, lines[7].Kind);
        Assert.Equal("1220x2712", lines[7].Detail);
    }

    [Fact]
    public void NoDevice()
    {
        var a = ScrcpyLogClassifier.Classify("ERROR: Could not find any ADB device");
        var b = ScrcpyLogClassifier.Classify("ERROR: Server connection failed");

        Assert.Equal(ScrcpyLogKind.NoDevice, a.Kind);
        Assert.Equal(ScrcpyLogLevel.Error, a.Level);
        Assert.Equal(ScrcpyLogKind.ServerConnectionFailed, b.Kind);
    }

    [Fact]
    public void DeviceDisconnected_IsWarningProblem()
    {
        var line = ScrcpyLogClassifier.Classify("WARN: Device disconnected");

        Assert.Equal(ScrcpyLogKind.DeviceDisconnected, line.Kind);
        Assert.Equal(ScrcpyLogLevel.Warning, line.Level);
        Assert.True(line.IsProblem);
    }

    [Theory]
    [InlineData("ERROR: Server terminated unexpectedly")]
    [InlineData("ERROR: Could not push server")]
    public void ServerErrors(string raw)
    {
        var line = ScrcpyLogClassifier.Classify(raw);
        Assert.Equal(ScrcpyLogKind.ServerError, line.Kind);
        Assert.Equal(ScrcpyLogLevel.Error, line.Level);
    }

    [Fact]
    public void PlainLine_IsOtherNone()
    {
        var line = ScrcpyLogClassifier.Classify("Dependencies (compiled / linked):");
        Assert.Equal(ScrcpyLogLevel.Other, line.Level);
        Assert.Equal(ScrcpyLogKind.None, line.Kind);
        Assert.False(line.IsProblem);
        Assert.Equal("Dependencies (compiled / linked):", line.Message);
    }

    [Fact]
    public void TrailingCarriageReturn_IsStripped()
    {
        var line = ScrcpyLogClassifier.Classify("INFO: Renderer: direct3d11\r");
        Assert.Equal("INFO: Renderer: direct3d11", line.Raw);
        Assert.Equal("direct3d11", line.Detail);
    }
}
