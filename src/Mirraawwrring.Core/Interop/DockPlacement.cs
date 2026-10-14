namespace Mirraawwrring.Core.Interop;

/// <summary>Persegi dalam piksel fisik layar (seperti RECT Win32: Right/Bottom eksklusif).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public static PixelRect FromSize(int left, int top, int width, int height) => new(left, top, left + width, top + height);
}

public enum DockSide
{
    Right,
    Left,
}

/// <summary>
/// Hitung posisi jendela toolbar yang menempel ke jendela scrcpy (§6.5). Fungsi murni dalam piksel fisik;
/// konversi DIP ↔ piksel dilakukan pemanggil dengan DPI monitor jendela scrcpy.
/// </summary>
public static class DockPlacement
{
    /// <summary>Jarak antara tepi jendela scrcpy dan toolbar, dalam DIP (mockup: 6 px).</summary>
    public const int GapDip = 6;

    /// <param name="frame">Bingkai jendela scrcpy yang terlihat (DWMWA_EXTENDED_FRAME_BOUNDS).</param>
    /// <param name="clientTop">Y layar dari tepi atas area klien scrcpy (toolbar sejajar dengan konten, bukan title bar).</param>
    /// <param name="workArea">Area kerja monitor tempat scrcpy berada (tanpa taskbar).</param>
    /// <param name="width">Lebar jendela toolbar (piksel).</param>
    /// <param name="height">Tinggi jendela toolbar (piksel).</param>
    /// <param name="gap">Jarak ke jendela scrcpy (piksel).</param>
    public static (PixelRect Rect, DockSide Side) Compute(PixelRect frame, int clientTop, PixelRect workArea, int width, int height, int gap)
    {
        var side = DockSide.Right;
        var left = frame.Right + gap;
        if (left + width > workArea.Right)
        {
            var leftSide = frame.Left - gap - width;
            if (leftSide >= workArea.Left)
            {
                side = DockSide.Left;
                left = leftSide;
            }
            else
            {
                // Tidak muat di kedua sisi (jendela scrcpy selebar layar): tempel di tepi kanan area kerja, menumpuk sedikit.
                left = Math.Max(workArea.Left, workArea.Right - width);
            }
        }

        var top = clientTop;
        if (top + height > workArea.Bottom)
        {
            top = workArea.Bottom - height;
        }

        if (top < workArea.Top)
        {
            top = workArea.Top;
        }

        return (PixelRect.FromSize(left, top, width, height), side);
    }

    public static int DipToPixels(double dip, uint dpi) => (int)Math.Round(dip * dpi / 96.0);

    public static double PixelsToDip(int pixels, uint dpi) => pixels * 96.0 / dpi;
}
