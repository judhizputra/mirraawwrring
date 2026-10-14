using System.Runtime.InteropServices;

namespace Mirraawwrring.App.Interop;

/// <summary>P/Invoke Win32 yang dipakai aplikasi. Tambahkan deklarasi baru di sini, jangan tersebar.</summary>
internal static class NativeMethods
{
    // DwmSetWindowAttribute
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    public static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
