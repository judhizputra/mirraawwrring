using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests.Devices;

public sealed class DeviceActionsTests : IDisposable
{
    private const string Serial = "R58N12ABCDE";
    private readonly FakeAdbClient _adb = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 16, 45, 12, TimeSpan.FromHours(7)));
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "mirraawwrring-test-" + Guid.NewGuid().ToString("N"));
    private readonly DeviceActions _actions;

    public DeviceActionsTests()
    {
        var paths = new FakeAppPaths { ScreenshotsDirectory = _tempDir };
        _actions = new DeviceActions(_adb, paths, _time, NullLogger<DeviceActions>.Instance);
    }

    [Fact]
    public async Task KeyEvents_UseDocumentedKeycodes()
    {
        await _actions.PowerAsync(Serial);
        await _actions.BackAsync(Serial);
        await _actions.HomeAsync(Serial);
        await _actions.RecentsAsync(Serial);
        await _actions.VolumeAsync(Serial, up: true, useFallback: false);
        await _actions.VolumeAsync(Serial, up: false, useFallback: false);

        Assert.Equal(
        [
            $"shell {Serial} input keyevent 26",
            $"shell {Serial} input keyevent 4",
            $"shell {Serial} input keyevent 3",
            $"shell {Serial} input keyevent 187",
            $"shell {Serial} input keyevent 24",
            $"shell {Serial} input keyevent 25",
        ], _adb.Calls);
    }

    [Fact]
    public async Task Volume_Fallback_UsesMediaSession()
    {
        await _actions.VolumeAsync(Serial, up: true, useFallback: true);
        await _actions.VolumeAsync(Serial, up: false, useFallback: true);

        Assert.Equal($"shell {Serial} cmd media_session volume --adj raise", _adb.Calls[0]);
        Assert.Equal($"shell {Serial} cmd media_session volume --adj lower", _adb.Calls[1]);
    }

    [Theory]
    [InlineData("0\n", 1)]
    [InlineData("3\n", 0)]
    [InlineData("null\n", 1)]
    [InlineData("", 1)]
    public async Task Rotate_DisablesAutoRotateThenSetsNext(string current, int expectedNext)
    {
        _adb.ShellResponse = (_, cmd) => cmd[1] == "get" ? ProcessResult.Ok(current) : ProcessResult.Ok();

        var result = await _actions.RotateAsync(Serial);

        Assert.True(result.Succeeded);
        Assert.Equal(
        [
            $"shell {Serial} settings get system user_rotation",
            $"shell {Serial} settings put system accelerometer_rotation 0",
            $"shell {Serial} settings put system user_rotation {expectedNext}",
        ], _adb.Calls);
    }

    [Fact]
    public async Task Rotate_StopsWhenDisablingAutoRotateFails()
    {
        _adb.ShellResponse = (_, cmd) => cmd.Contains("accelerometer_rotation") ? ProcessResult.Fail(1, "", "denied") : ProcessResult.Ok("0");

        var result = await _actions.RotateAsync(Serial);

        Assert.False(result.Succeeded);
        Assert.Equal(2, _adb.Calls.Count);
    }

    [Fact]
    public async Task Notifications_ExpandAndCollapse()
    {
        await _actions.SetNotificationsAsync(Serial, expand: true);
        await _actions.SetNotificationsAsync(Serial, expand: false);

        Assert.Equal($"shell {Serial} cmd statusbar expand-notifications", _adb.Calls[0]);
        Assert.Equal($"shell {Serial} cmd statusbar collapse", _adb.Calls[1]);
    }

    [Fact]
    public async Task ShowTouches_PutAndGet()
    {
        _adb.ShellResponse = (_, cmd) => cmd[1] == "get" ? ProcessResult.Ok("1\n") : ProcessResult.Ok();

        await _actions.SetShowTouchesAsync(Serial, on: true);
        var on = await _actions.GetShowTouchesAsync(Serial);

        Assert.True(on);
        Assert.Equal($"shell {Serial} settings put system show_touches 1", _adb.Calls[0]);
        Assert.Equal($"shell {Serial} settings get system show_touches", _adb.Calls[1]);
    }

    [Fact]
    public async Task Screenshot_WritesPngAndReturnsBytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        _adb.RunResponse = _ => new ProcessResult(0, "", "", TimeSpan.Zero, false, png);

        var result = await _actions.ScreenshotAsync(Serial);

        Assert.True(result.Success);
        Assert.Equal($"run -s {Serial} exec-out screencap -p", _adb.Calls.Single());
        Assert.Equal(Path.Combine(_tempDir, "screenshot_20261014_164512.png"), result.FilePath);
        Assert.Equal(png, await File.ReadAllBytesAsync(result.FilePath!));
        Assert.Same(png, result.Png);
    }

    [Fact]
    public async Task Screenshot_SameSecondTwice_GetsSuffix()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        _adb.RunResponse = _ => new ProcessResult(0, "", "", TimeSpan.Zero, false, png);

        var first = await _actions.ScreenshotAsync(Serial);
        var second = await _actions.ScreenshotAsync(Serial);

        Assert.EndsWith("screenshot_20261014_164512.png", first.FilePath);
        Assert.EndsWith("screenshot_20261014_164512-2.png", second.FilePath);
    }

    [Theory]
    [InlineData(0, new byte[] { 1, 2, 3 })]
    [InlineData(1, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData(0, new byte[0])]
    public async Task Screenshot_InvalidOutput_Fails(int exitCode, byte[] bytes)
    {
        _adb.RunResponse = _ => new ProcessResult(exitCode, "", "error: device offline", TimeSpan.Zero, false, bytes);

        var result = await _actions.ScreenshotAsync(Serial);

        Assert.False(result.Success);
        Assert.Null(result.FilePath);
        Assert.False(Directory.Exists(_tempDir) && Directory.EnumerateFiles(_tempDir).Any());
    }

    [Fact]
    public async Task InstallApk_UsesReinstallFlag()
    {
        await _actions.InstallApkAsync(Serial, @"C:\apps\contoh app.apk");

        Assert.Equal($"run -s {Serial} install -r C:\\apps\\contoh app.apk", _adb.Calls.Single());
    }

    [Fact]
    public async Task DeviceInfoText_CombinesGetpropAndWmSize()
    {
        _adb.ShellResponse = (_, cmd) => cmd[0] == "wm" ? ProcessResult.Ok("Physical size: 1220x2712\n") : ProcessResult.Ok();

        var text = await _actions.GetDeviceInfoTextAsync(Serial);

        Assert.Equal(string.Join(Environment.NewLine, [$"Serial: {Serial}", "Manufacturer: Xiaomi", "Model: 2407FPN8EG", "Android: 16", "Display: 1220x2712"]), text);
    }

    [Fact]
    public void RequiresInjection_MatchesSpecTable()
    {
        Assert.Contains(DeviceAction.Power, DeviceActions.RequiresInjection);
        Assert.Contains(DeviceAction.Back, DeviceActions.RequiresInjection);
        Assert.Contains(DeviceAction.SendClipboard, DeviceActions.RequiresInjection);
        Assert.DoesNotContain(DeviceAction.Rotate, DeviceActions.RequiresInjection);
        Assert.DoesNotContain(DeviceAction.Notifications, DeviceActions.RequiresInjection);
        Assert.DoesNotContain(DeviceAction.Screenshot, DeviceActions.RequiresInjection);
        Assert.DoesNotContain(DeviceAction.Record, DeviceActions.RequiresInjection);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
