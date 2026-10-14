using System.Runtime.InteropServices;
using Mirraawwrring.Core.Interop;

namespace Mirraawwrring.App.Interop;

/// <summary>Pembacaan geometri jendela lain (scrcpy) dalam piksel fisik, plus gaya jendela toolbar.</summary>
internal static class WindowGeometry
{
    /// <summary>Bingkai yang terlihat (tanpa border tak terlihat Windows 10/11); fallback GetWindowRect.</summary>
    public static PixelRect GetVisibleFrame(nint hwnd)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<NativeMethods.RECT>()) == 0)
        {
            return ToPixelRect(frame);
        }

        NativeMethods.GetWindowRect(hwnd, out var rect);
        return ToPixelRect(rect);
    }

    /// <summary>Area klien dalam koordinat layar.</summary>
    public static PixelRect GetClientArea(nint hwnd)
    {
        NativeMethods.GetClientRect(hwnd, out var client);
        var origin = new NativeMethods.POINT();
        NativeMethods.ClientToScreen(hwnd, ref origin);
        return PixelRect.FromSize(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
    }

    /// <summary>Area kerja monitor terdekat (tanpa taskbar).</summary>
    public static PixelRect GetWorkArea(nint hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor != 0 && NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return ToPixelRect(info.rcWork);
        }

        return new PixelRect(0, 0, (int)System.Windows.SystemParameters.PrimaryScreenWidth, (int)System.Windows.SystemParameters.PrimaryScreenHeight);
    }

    public static uint GetDpi(nint hwnd)
    {
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi == 0 ? 96 : dpi;
    }

    public static void MoveTo(nint hwnd, int x, int y) =>
        NativeMethods.SetWindowPos(hwnd, 0, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    /// <summary>Jendela alat: tidak muncul di Alt+Tab dan tidak pernah mengambil fokus keyboard dari scrcpy.</summary>
    public static void MakeNoActivateToolWindow(nint hwnd)
    {
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    private static PixelRect ToPixelRect(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);
}
