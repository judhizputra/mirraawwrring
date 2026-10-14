using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mirraawwrring.App.Interop;

/// <summary>
/// Title bar native dalam mode gelap (PROMPT.md §7): memakai DwmSetWindowAttribute, bukan title bar buatan sendiri.
/// Panggil <see cref="Apply"/> dari handler <c>SourceInitialized</c> jendela.
/// </summary>
public static class WindowTheming
{
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
        {
            return;
        }

        var dark = 1;
        // Windows 10 20H1+ memakai atribut 20; 1809..1909 memakai 19. Coba keduanya, abaikan kegagalan.
        if (NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
        {
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
        }

        // Windows 11 (22000+): warnai caption sesuai mockup (#111317) dan teksnya (#C9CDD4). Di Windows 10 panggilan ini gagal diam-diam.
        if (window.TryFindResource("Color.TitleBar") is Color caption)
        {
            var captionRef = ToColorRef(caption);
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ref captionRef, sizeof(int));
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref captionRef, sizeof(int));
        }

        if (window.TryFindResource("Color.TextSecondary") is Color text)
        {
            var textRef = ToColorRef(text);
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_TEXT_COLOR, ref textRef, sizeof(int));
        }
    }

    /// <summary>COLORREF = 0x00BBGGRR.</summary>
    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
}
