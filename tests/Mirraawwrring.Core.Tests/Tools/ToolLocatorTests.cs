using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Tests.Tools;

public sealed class ToolLocatorTests
{
    private const string Exe = @"C:\apps\Mirraawwrring";
    private readonly FakeAppPaths _paths = new(Exe);
    private readonly FakeFileSystem _fs = new();

    private ToolLocation Locate(string? custom = null) => new ToolLocator(_paths, _fs).Locate(custom);

    [Fact]
    public void NothingInstalled_ReturnsIncomplete()
    {
        var loc = Locate();

        Assert.False(loc.HasScrcpy);
        Assert.False(loc.HasAdb);
        Assert.False(loc.IsComplete);
        Assert.Empty(loc.OtherAdbPaths);
    }

    [Fact]
    public void BundledFolder_WinsOverEverything()
    {
        _fs.AddFile(@"C:\apps\Mirraawwrring\scrcpy\scrcpy.exe", @"C:\apps\Mirraawwrring\scrcpy\adb.exe")
           .AddFile(@"C:\apps\Mirraawwrring\scrcpy.exe", @"C:\apps\Mirraawwrring\adb.exe")
           .AddFile(@"C:\scrcpy-manual\scrcpy.exe")
           .SetEnv("PATH", @"C:\Windows;C:\other\scrcpy");
        _fs.AddFile(@"C:\other\scrcpy\scrcpy.exe", @"C:\other\scrcpy\adb.exe");

        var loc = Locate(@"C:\scrcpy-manual");

        Assert.Equal(@"C:\apps\Mirraawwrring\scrcpy\scrcpy.exe", loc.ScrcpyPath);
        Assert.Equal(ToolSource.BundledFolder, loc.ScrcpySource);
        Assert.Equal(@"C:\apps\Mirraawwrring\scrcpy\adb.exe", loc.AdbPath);
        Assert.Equal(ToolSource.ScrcpyFolder, loc.AdbSource);
        Assert.Equal(@"C:\apps\Mirraawwrring\scrcpy\scrcpy-server", loc.ScrcpyServerPath);
        Assert.Equal([@"C:\other\scrcpy\adb.exe"], loc.OtherAdbPaths);
    }

    [Fact]
    public void ExeFolder_SecondPriority()
    {
        _fs.AddFile(@"C:\apps\Mirraawwrring\scrcpy.exe", @"C:\apps\Mirraawwrring\adb.exe");

        var loc = Locate();

        Assert.Equal(ToolSource.ExeFolder, loc.ScrcpySource);
        Assert.Equal(@"C:\apps\Mirraawwrring\adb.exe", loc.AdbPath);
    }

    [Theory]
    [InlineData(@"D:\tools\scrcpy")]
    [InlineData(@"D:\tools\scrcpy\")]
    [InlineData(@"D:\tools\scrcpy\scrcpy.exe")]
    [InlineData(@"""D:\tools\scrcpy""")]
    public void CustomPath_AcceptsFolderOrExe(string custom)
    {
        _fs.AddFile(@"D:\tools\scrcpy\scrcpy.exe", @"D:\tools\scrcpy\adb.exe");

        var loc = Locate(custom);

        Assert.Equal(@"D:\tools\scrcpy\scrcpy.exe", loc.ScrcpyPath);
        Assert.Equal(ToolSource.CustomPath, loc.ScrcpySource);
        Assert.Equal(@"D:\tools\scrcpy\adb.exe", loc.AdbPath);
    }

    [Fact]
    public void Winget_ResolvesSymlinkAndFindsAdbNextToTarget()
    {
        const string target = @"C:\Users\u\AppData\Local\Microsoft\WinGet\Packages\Genymobile.scrcpy_x\scrcpy-win64-v4.1\scrcpy.exe";
        _fs.AddLink(_paths.WingetScrcpyLink, target)
           .AddFile(target, Path.Combine(Path.GetDirectoryName(target)!, "adb.exe"));

        var loc = Locate();

        Assert.Equal(target, loc.ScrcpyPath);
        Assert.Equal(ToolSource.Winget, loc.ScrcpySource);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(target)!, "adb.exe"), loc.AdbPath);
        Assert.Equal(ToolSource.ScrcpyFolder, loc.AdbSource);
    }

    [Fact]
    public void Path_LastResort_AndOtherAdbReported()
    {
        _fs.SetEnv("PATH", @"C:\Windows;""C:\Program Files\scrcpy"";C:\Android\platform-tools;")
           .AddFile(@"C:\Program Files\scrcpy\scrcpy.exe", @"C:\Program Files\scrcpy\adb.exe")
           .AddFile(@"C:\Android\platform-tools\adb.exe")
           .AddFile(_paths.AndroidSdkAdb);

        var loc = Locate();

        Assert.Equal(@"C:\Program Files\scrcpy\scrcpy.exe", loc.ScrcpyPath);
        Assert.Equal(ToolSource.Path, loc.ScrcpySource);
        Assert.Equal(@"C:\Program Files\scrcpy\adb.exe", loc.AdbPath);
        Assert.Equal([@"C:\Android\platform-tools\adb.exe", _paths.AndroidSdkAdb], loc.OtherAdbPaths);
    }

    [Fact]
    public void ScrcpyWithoutAdbNextToIt_FallsBackToOtherAdb()
    {
        _fs.AddFile(@"C:\apps\Mirraawwrring\scrcpy\scrcpy.exe")
           .AddFile(_paths.AndroidSdkAdb);

        var loc = Locate();

        Assert.True(loc.HasScrcpy);
        Assert.Equal(_paths.AndroidSdkAdb, loc.AdbPath);
        Assert.Equal(ToolSource.AndroidSdk, loc.AdbSource);
        Assert.Empty(loc.OtherAdbPaths);
    }

    [Fact]
    public void SameAdbViaTwoRoutes_NotReportedAsOther()
    {
        _fs.AddFile(@"C:\apps\Mirraawwrring\scrcpy\scrcpy.exe", @"C:\apps\Mirraawwrring\scrcpy\adb.exe")
           .SetEnv("PATH", @"c:\APPS\mirraawwrring\SCRCPY");

        var loc = Locate();

        Assert.Empty(loc.OtherAdbPaths);
    }
}
