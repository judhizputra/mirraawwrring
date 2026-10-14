using System.Globalization;

namespace Mirraawwrring.Core.Input;

/// <summary>Modifier hotkey; nilainya sama dengan MOD_* untuk <c>RegisterHotKey</c>.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// Hotkey milik aplikasi (screenshot, default Ctrl+Shift+S) dari teks di settings.json. Fungsi murni, tanpa Win32.
/// Format: modifier dipisah '+', diakhiri satu tombol: huruf, angka, F1–F24, atau nama (Space, Insert, Delete, PageUp, …).
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int VirtualKey, string KeyName)
{
    public const string Default = "Ctrl+Shift+S";

    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["PrintScreen"] = 0x2C,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Backspace"] = 0x08,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Pause"] = 0x13,
        ["Escape"] = 0x1B,
    };

    /// <summary>Teks kanonis, mis. "Ctrl+Shift+S".</summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(HotkeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Win))
            {
                parts.Add("Win");
            }

            parts.Add(KeyName);
            return string.Join('+', parts);
        }
    }

    public static Hotkey ParseOrDefault(string? text) => TryParse(text, out var hotkey) ? hotkey : TryParse(Default, out var def) ? def : throw new InvalidOperationException();

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var mod = tokens[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" or "STRG" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "SUPER" or "META" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };
            if (mod == HotkeyModifiers.None)
            {
                return false;
            }

            modifiers |= mod;
        }

        var key = tokens[^1];
        if (!TryParseKey(key, out var vk, out var name))
        {
            return false;
        }

        // Tanpa modifier, huruf/angka akan membajak pengetikan normal; wajib minimal satu modifier.
        if (modifiers == HotkeyModifiers.None && vk is >= 0x30 and <= 0x5A)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, vk, name);
        return true;
    }

    private static bool TryParseKey(string key, out int virtualKey, out string name)
    {
        virtualKey = 0;
        name = string.Empty;
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                virtualKey = c;
                name = c.ToString();
                return true;
            }

            return false;
        }

        if ((key[0] == 'F' || key[0] == 'f') && int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 24)
        {
            virtualKey = 0x70 + f - 1;
            name = "F" + f.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (NamedKeys.TryGetValue(key, out var named))
        {
            virtualKey = named;
            name = NamedKeys.Keys.First(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        return false;
    }
}
