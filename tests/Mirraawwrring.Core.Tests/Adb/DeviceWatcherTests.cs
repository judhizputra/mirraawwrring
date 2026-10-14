using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Tests.Adb;

public sealed class DeviceWatcherTests
{
    [Fact]
    public async Task Refresh_RaisesChangedOnlyWhenListDiffers()
    {
        var adb = new FakeAdbClient()
            .Enqueue(TestDevices.Usb())
            .Enqueue(TestDevices.Usb())
            .Enqueue(TestDevices.Usb(state: DeviceState.Unauthorized))
            .Enqueue(TestDevices.Usb(state: DeviceState.Unauthorized), TestDevices.Wifi())
            .Enqueue();
        await using var watcher = new DeviceWatcher(adb, NullLogger<DeviceWatcher>.Instance);
        var changes = new List<int>();
        watcher.DevicesChanged += list => changes.Add(list.Count);

        for (var i = 0; i < 5; i++)
        {
            await watcher.RefreshAsync();
        }

        Assert.Equal([1, 1, 2, 0], changes);
        Assert.Empty(watcher.Devices);
    }

    [Fact]
    public async Task Refresh_KeepsLastListAndReportsFailureOnce()
    {
        var adb = new FakeAdbClient()
            .Enqueue(TestDevices.Usb())
            .EnqueueFailure(new InvalidOperationException("adb hilang"))
            .EnqueueFailure(new InvalidOperationException("adb masih hilang"))
            .Enqueue(TestDevices.Usb());
        await using var watcher = new DeviceWatcher(adb, NullLogger<DeviceWatcher>.Instance);
        var failures = 0;
        watcher.PollFailed += _ => failures++;

        await watcher.RefreshAsync();
        await watcher.RefreshAsync();
        await watcher.RefreshAsync();
        await watcher.RefreshAsync();

        Assert.Equal(1, failures);
        Assert.Single(watcher.Devices);
    }

    [Fact]
    public async Task StartStop_PollsImmediatelyThenOnInterval()
    {
        var adb = new FakeAdbClient().Enqueue(TestDevices.Usb());
        await using var watcher = new DeviceWatcher(adb, NullLogger<DeviceWatcher>.Instance, interval: TimeSpan.FromMilliseconds(30));
        var changed = new TaskCompletionSource();
        watcher.DevicesChanged += _ => changed.TrySetResult();

        watcher.Start();
        Assert.True(watcher.IsRunning);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(120);
        await watcher.StopAsync();

        Assert.False(watcher.IsRunning);
        Assert.True(adb.GetDevicesCalls >= 2, $"hanya {adb.GetDevicesCalls} poll");
        Assert.Single(watcher.Devices);
    }

    [Fact]
    public void SameList_IgnoresOrder()
    {
        var a = new[] { TestDevices.Usb(), TestDevices.Wifi() };
        var b = new[] { TestDevices.Wifi(), TestDevices.Usb() };
        Assert.True(DeviceWatcher.SameList(a, b));
        Assert.False(DeviceWatcher.SameList(a, [TestDevices.Usb()]));
        Assert.False(DeviceWatcher.SameList(a, [TestDevices.Usb(model: "lain"), TestDevices.Wifi()]));
    }
}
