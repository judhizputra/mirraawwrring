using Mirraawwrring.Core.Wireless;

namespace Mirraawwrring.Core.Tests.Wireless;

public sealed class AddressUtilTests
{
    [Theory]
    [InlineData("192.168.1.20:37099", "192.168.1.20", 37099)]
    [InlineData("  hp.local:5555 ", "hp.local", 5555)]
    public void TryParseHostPort_Valid(string input, string host, int port)
    {
        Assert.True(AddressUtil.TryParseHostPort(input, out var h, out var p));
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("192.168.1.20:")]
    [InlineData("192.168.1.20:0")]
    [InlineData("192.168.1.20:70000")]
    [InlineData("192.168.1.20:abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("192.168.1.20:41235 extra")]
    public void TryParseHostPort_Invalid(string? input)
    {
        Assert.False(AddressUtil.TryParseHostPort(input, out _, out _));
    }

    [Theory]
    [InlineData("192.168.1.20", "192.168.1.20:5555")]
    [InlineData("192.168.1.20:41235", "192.168.1.20:41235")]
    [InlineData(" 10.0.0.5 ", "10.0.0.5:5555")]
    public void NormalizeConnectAddress_AddsDefaultPort(string input, string expected)
    {
        Assert.Equal(expected, AddressUtil.NormalizeConnectAddress(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.20:99999")]
    [InlineData("bukan alamat!")]
    public void NormalizeConnectAddress_Invalid(string input)
    {
        Assert.Null(AddressUtil.NormalizeConnectAddress(input));
    }

    [Theory]
    [InlineData("482113", "482113")]
    [InlineData("482 113", "482113")]
    [InlineData("48-21-13", "482113")]
    [InlineData(" 482113\n", "482113")]
    public void NormalizePairingCode_Valid(string input, string expected)
    {
        Assert.Equal(expected, AddressUtil.NormalizePairingCode(input));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("")]
    public void NormalizePairingCode_Invalid(string input)
    {
        Assert.Null(AddressUtil.NormalizePairingCode(input));
    }

    [Fact]
    public void HostOf()
    {
        Assert.Equal("192.168.1.20", AddressUtil.HostOf("192.168.1.20:5555"));
        Assert.Equal("192.168.1.20", AddressUtil.HostOf("192.168.1.20"));
    }
}
