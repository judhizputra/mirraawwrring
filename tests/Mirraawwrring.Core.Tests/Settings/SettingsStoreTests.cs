using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Scrcpy;
using Mirraawwrring.Core.Settings;

namespace Mirraawwrring.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mirraawwrring-tests", Guid.NewGuid().ToString("N"));
    private readonly IAppPaths _paths;

    public SettingsStoreTests()
    {
        _paths = new AppPaths(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private SettingsStore NewStore() => new(_paths, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var s = NewStore().Current;

        Assert.Equal(MirrorMode.MirrorAndControl, s.Options.Mode);
        Assert.Equal(8, s.Options.VideoBitRateMbps);
        Assert.True(s.Options.ForwardAudio);
        Assert.Null(s.CustomScrcpyPath);
        Assert.Empty(s.ConnectionHistory);
        Assert.True(s.MinimizeToTray);
    }

    [Fact]
    public void Update_PersistsAndReloads()
    {
        var store = NewStore();
        store.Update(s =>
        {
            s.Options = s.Options with { Mode = MirrorMode.DataSaver, MaxSize = 1280, MaxFps = null, UhidInput = true, RecordToFile = true };
            s.LastDeviceSerial = "R58N12ABCDE";
            s.ConnectionHistory.Add("192.168.1.20:5555");
            s.CustomScrcpyPath = @"D:\tools\scrcpy";
            s.MinimizeToTray = false;
        });

        Assert.True(File.Exists(_paths.SettingsFile));
        var reloaded = NewStore().Current;
        Assert.Equal(MirrorMode.DataSaver, reloaded.Options.Mode);
        Assert.Equal(1280, reloaded.Options.MaxSize);
        Assert.Null(reloaded.Options.MaxFps);
        Assert.True(reloaded.Options.UhidInput);
        Assert.True(reloaded.Options.RecordToFile);
        Assert.Equal("R58N12ABCDE", reloaded.LastDeviceSerial);
        Assert.Equal(["192.168.1.20:5555"], reloaded.ConnectionHistory);
        Assert.Equal(@"D:\tools\scrcpy", reloaded.CustomScrcpyPath);
        Assert.False(reloaded.MinimizeToTray);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsFile)!);
        File.WriteAllText(_paths.SettingsFile, "{ ini bukan json");

        var s = NewStore().Current;

        Assert.Equal(MirrorMode.MirrorAndControl, s.Options.Mode);
    }

    [Fact]
    public void UnknownPropertiesAndEnumAsString_AreTolerated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsFile)!);
        File.WriteAllText(_paths.SettingsFile, """{ "version": 1, "options": { "mode": "ViewOnly", "fiturBaru": 1 }, "propertiLama": true }""");

        var s = NewStore().Current;

        Assert.Equal(MirrorMode.ViewOnly, s.Options.Mode);
    }
}
