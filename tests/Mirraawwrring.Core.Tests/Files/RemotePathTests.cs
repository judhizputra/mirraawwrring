using Mirraawwrring.Core.Files;

namespace Mirraawwrring.Core.Tests.Files;

public sealed class RemotePathTests
{
    [Theory]
    [InlineData("/sdcard", "DCIM", "/sdcard/DCIM")]
    [InlineData("/sdcard/", "DCIM", "/sdcard/DCIM")]
    [InlineData("/", "sdcard", "/sdcard")]
    public void Combine(string dir, string name, string expected) => Assert.Equal(expected, RemotePath.Combine(dir, name));

    [Theory]
    [InlineData("/sdcard/DCIM/Camera", "Camera")]
    [InlineData("/sdcard/DCIM/Camera/", "Camera")]
    [InlineData("/sdcard", "sdcard")]
    [InlineData("file.txt", "file.txt")]
    public void NameOf(string path, string expected) => Assert.Equal(expected, RemotePath.NameOf(path));

    [Theory]
    [InlineData("/sdcard/DCIM/Camera", "/sdcard/DCIM")]
    [InlineData("/sdcard/DCIM", "/sdcard")]
    [InlineData("/sdcard", null)]
    [InlineData("/sdcard/", null)]
    [InlineData("/data", "/")]
    public void ParentOf(string path, string? expected) => Assert.Equal(expected, RemotePath.ParentOf(path));

    [Theory]
    [InlineData("/sdcard//DCIM/", "/sdcard/DCIM")]
    [InlineData("/sdcard/DCIM/../Pictures/./x", "/sdcard/Pictures/x")]
    [InlineData("sdcard", "/sdcard")]
    [InlineData("/../..", "/")]
    public void Normalize(string path, string expected) => Assert.Equal(expected, RemotePath.Normalize(path));

    [Fact]
    public void Breadcrumb_InsideStorage()
    {
        var crumbs = RemotePath.Breadcrumb("/sdcard/DCIM/Camera");
        Assert.Equal([("/sdcard", "/sdcard"), ("DCIM", "/sdcard/DCIM"), ("Camera", "/sdcard/DCIM/Camera")], crumbs);
    }

    [Fact]
    public void Breadcrumb_Root()
    {
        Assert.Equal([("/sdcard", "/sdcard")], RemotePath.Breadcrumb("/sdcard"));
    }

    [Fact]
    public void Breadcrumb_OutsideStorage()
    {
        Assert.Equal([("/", "/"), ("data", "/data"), ("local", "/data/local")], RemotePath.Breadcrumb("/data/local"));
    }

    [Theory]
    [InlineData("foto.jpg", true)]
    [InlineData("Folder Baru", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    public void IsValidName(string name, bool expected) => Assert.Equal(expected, RemotePath.IsValidName(name));

    [Fact]
    public void Shortcuts_UnderRoot()
    {
        Assert.All(RemotePath.Shortcuts, s => Assert.StartsWith("/sdcard/", s.Path));
        Assert.Equal(["Download", "DCIM", "Pictures", "Documents", "Movies"], RemotePath.Shortcuts.Select(s => s.Name));
    }
}
