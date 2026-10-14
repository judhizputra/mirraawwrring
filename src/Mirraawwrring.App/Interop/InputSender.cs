using Mirraawwrring.Core.Devices;

namespace Mirraawwrring.App.Interop;

/// <summary>
/// Mengirim kombinasi tombol ke jendela scrcpy lewat <c>SendInput</c> (§5.8 "Kirim clipboard": fokuskan jendela scrcpy lalu Ctrl+V).
/// Hanya berhasil kalau proses ini baru menerima input (klik tombol toolbar) sehingga boleh memindahkan foreground.
/// </summary>
internal static class InputSender
{
    public static async Task<bool> SendChordAsync(nint targetHwnd, ScrcpyKeyChord chord)
    {
        if (targetHwnd == 0 || !NativeMethods.IsWindow(targetHwnd))
        {
            return false;
        }

        if (NativeMethods.GetForegroundWindow() != targetHwnd)
        {
            NativeMethods.SetForegroundWindow(targetHwnd);
            await Task.Delay(120);
            if (NativeMethods.GetForegroundWindow() != targetHwnd)
            {
                return false;
            }
        }

        var modifiers = new List<ushort>(3);
        if (chord.Control)
        {
            modifiers.Add(NativeMethods.VK_CONTROL);
        }

        if (chord.Alt)
        {
            modifiers.Add(NativeMethods.VK_MENU);
        }

        if (chord.Shift)
        {
            modifiers.Add(NativeMethods.VK_SHIFT);
        }

        var key = (ushort)char.ToUpperInvariant(chord.Key);
        var inputs = new List<NativeMethods.INPUT>((modifiers.Count + 1) * 2);
        foreach (var m in modifiers)
        {
            inputs.Add(Key(m, up: false));
        }

        inputs.Add(Key(key, up: false));
        inputs.Add(Key(key, up: true));
        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            inputs.Add(Key(modifiers[i], up: true));
        }

        var array = inputs.ToArray();
        var sent = NativeMethods.SendInput((uint)array.Length, array, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == array.Length;
    }

    private static NativeMethods.INPUT Key(ushort vk, bool up) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = up ? NativeMethods.KEYEVENTF_KEYUP : 0 },
        },
    };
}
