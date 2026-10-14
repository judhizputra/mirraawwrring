using Mirraawwrring.Core.Files;

namespace Mirraawwrring.Core.Tests.Files;

public sealed class RemoteListingParserTests
{
    // Output nyata toybox: find /sdcard/DCIM/Camera -maxdepth 1 -mindepth 1 -exec stat -c '%F|%s|%Y|%n' {} +
    private const string Sample =
        "regular file|3355443|1760427012|/sdcard/DCIM/Camera/IMG_20261014_143012.jpg\n" +
        "directory|4096|1760000000|/sdcard/DCIM/Camera/.thumbnails\n" +
        "regular file|50954240|1760354725|/sdcard/DCIM/Camera/VID_20261013_191205.mp4\n" +
        "regular empty file|0|1760354725|/sdcard/DCIM/Camera/.nomedia\n" +
        "symbolic link|21|1760354725|/sdcard/DCIM/Camera/link\n" +
        "regular file|12|1760354725|/sdcard/DCIM/Camera/nama|dengan|pipa.txt\n";

    [Fact]
    public void Parse_RealOutput_SortsDirectoriesFirstThenName()
    {
        var result = RemoteListingParser.Parse(Sample);

        Assert.Empty(result.Errors);
        Assert.Equal(
        [
            ".thumbnails", ".nomedia", "IMG_20261014_143012.jpg", "link", "nama|dengan|pipa.txt", "VID_20261013_191205.mp4",
        ], result.Entries.Select(e => e.Name));
        var dir = result.Entries[0];
        Assert.True(dir.IsDirectory);
        Assert.Equal(0, dir.Size);
        var img = result.Entries.Single(e => e.Name.StartsWith("IMG"));
        Assert.False(img.IsDirectory);
        Assert.Equal(3355443, img.Size);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760427012).ToLocalTime(), img.Modified);
        Assert.Equal("/sdcard/DCIM/Camera/IMG_20261014_143012.jpg", img.Path);
    }

    [Fact]
    public void Parse_NameWithPipe_KeptIntact()
    {
        Assert.True(RemoteListingParser.TryParseLine("regular file|12|1760354725|/sdcard/a|b|c.txt", out var entry));
        Assert.Equal("/sdcard/a|b|c.txt", entry.Path);
        Assert.Equal("a|b|c.txt", entry.Name);
    }

    [Fact]
    public void Parse_ErrorLines_CollectedNotThrown()
    {
        var output = "find: /sdcard/Android/data: Permission denied\r\nregular file|1|1760354725|/sdcard/x\n\n";

        var result = RemoteListingParser.Parse(output);

        Assert.Single(result.Entries);
        Assert.Equal(["find: /sdcard/Android/data: Permission denied"], result.Errors);
    }

    [Fact]
    public void Parse_Empty_NoEntries()
    {
        var result = RemoteListingParser.Parse("");
        Assert.Empty(result.Entries);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("regular file|abc|1|/x")]
    [InlineData("regular file|1|x|/x")]
    [InlineData("regular file|1|1|")]
    [InlineData("regular file|1")]
    public void TryParseLine_Invalid(string line)
    {
        Assert.False(RemoteListingParser.TryParseLine(line, out _));
    }
}
