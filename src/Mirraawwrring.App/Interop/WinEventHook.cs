namespace Mirraawwrring.App.Interop;

/// <summary>
/// Pembungkus <c>SetWinEventHook</c> (out-of-context). Harus dibuat dan dilepas di thread yang punya message loop (UI thread);
/// callback juga tiba di thread itu. Delegate disimpan di field agar tidak dikumpulkan GC selama hook hidup.
/// </summary>
internal sealed class WinEventHook : IDisposable
{
    private readonly NativeMethods.WinEventProc _callback;
    private nint _hook;

    /// <param name="processId">0 = semua proses.</param>
    public WinEventHook(uint eventMin, uint eventMax, uint processId, Action<uint, nint, int> handler)
    {
        _callback = (_, eventType, hwnd, idObject, _, _, _) => handler(eventType, hwnd, idObject);
        _hook = NativeMethods.SetWinEventHook(eventMin, eventMax, 0, _callback, processId, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    public bool IsActive => _hook != 0;

    public void Dispose()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = 0;
        }
    }
}
