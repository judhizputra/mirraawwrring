using System.Text;

namespace Mirraawwrring.Core.Interop;

/// <summary>Mencari jendela utama milik sebuah PID (jendela scrcpy) lewat EnumWindows + GetWindowThreadProcessId (§5.6).</summary>
public static class WindowFinder
{
    /// <summary>HWND jendela top-level yang terlihat, tanpa owner, milik <paramref name="processId"/>; 0 jika belum ada.</summary>
    public static nint FindMainWindow(int processId)
    {
        nint found = 0;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != (uint)processId || !NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != 0 || NativeMethods.GetWindowTextLength(hwnd) == 0)
            {
                return true;
            }

            found = hwnd;
            return false;
        }, 0);
        return found;
    }

    public static string GetWindowTitle(nint hwnd)
    {
        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Polling tiap 100 ms sampai jendela muncul atau timeout; 0 jika timeout/proses selesai.</summary>
    public static async Task<nint> WaitForMainWindowAsync(int processId, TimeSpan timeout, Func<bool>? processExited = null, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hwnd = FindMainWindow(processId);
            if (hwnd != 0)
            {
                return hwnd;
            }

            if (processExited?.Invoke() == true)
            {
                return 0;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }
}
