using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Tests.Adb;

public sealed class ShellQuotingTests
{
    [Theory]
    [InlineData("input", "input")]
    [InlineData("/sdcard/DCIM/Camera", "/sdcard/DCIM/Camera")]
    [InlineData("accelerometer_rotation", "accelerometer_rotation")]
    [InlineData("-c", "-c")]
    [InlineData("%F|%s", "'%F|%s'")]
    [InlineData("", "''")]
    [InlineData("nama dengan spasi.jpg", "'nama dengan spasi.jpg'")]
    [InlineData("it's", "'it'\\''s'")]
    [InlineData("a\"b", "'a\"b'")]
    [InlineData("$HOME", "'$HOME'")]
    [InlineData("foto (1).jpg", "'foto (1).jpg'")]
    [InlineData("日本語.txt", "'日本語.txt'")]
    public void Quote(string input, string expected)
    {
        Assert.Equal(expected, ShellQuoting.Quote(input));
    }

    [Fact]
    public void Join_QuotesEachArgument()
    {
        var joined = ShellQuoting.Join(["mv", "/sdcard/a b.txt", "/sdcard/c.txt"]);
        Assert.Equal("mv '/sdcard/a b.txt' /sdcard/c.txt", joined);
    }
}
