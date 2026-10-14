using Mirraawwrring.Core.Input;

namespace Mirraawwrring.Core.Tests.Input;

public sealed class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Shift+S", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53, "Ctrl+Shift+S")]
    [InlineData("ctrl + shift + s", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53, "Ctrl+Shift+S")]
    [InlineData("Control+Alt+P", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x50, "Ctrl+Alt+P")]
    [InlineData("Win+Shift+F12", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x7B, "Shift+Win+F12")]
    [InlineData("Alt+PrintScreen", HotkeyModifiers.Alt, 0x2C, "Alt+PrintScreen")]
    [InlineData("F9", HotkeyModifiers.None, 0x78, "F9")]
    [InlineData("Ctrl+3", HotkeyModifiers.Control, 0x33, "Ctrl+3")]
    public void Parse_Valid(string text, HotkeyModifiers modifiers, int vk, string display)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(modifiers, hotkey.Modifiers);
        Assert.Equal(vk, hotkey.VirtualKey);
        Assert.Equal(display, hotkey.Display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("S")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Foo+S")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Hyper")]
    public void Parse_Invalid(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void ParseOrDefault_FallsBackToCtrlShiftS()
    {
        var hotkey = Hotkey.ParseOrDefault("tidak valid");
        Assert.Equal("Ctrl+Shift+S", hotkey.Display);
        Assert.Equal(0x53, hotkey.VirtualKey);
    }
}
