using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.Resources;
using Mirraawwrring.App.ViewModels;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Interop;
using Mirraawwrring.Core.Scrcpy;

namespace Mirraawwrring.App.Views;

/// <summary>
/// Toolbar samping untuk satu sesi scrcpy (§6.5): owner = HWND scrcpy (selalu di atasnya, ikut minimize), menempel di kanan
/// (atau kiri bila tidak muat) dan mengikuti pergerakan lewat SetWinEventHook. Hotkey screenshot hanya aktif saat scrcpy/toolbar jadi foreground.
/// Jendela ini tidak pernah mengambil fokus keyboard (WS_EX_NOACTIVATE) supaya mengetik di scrcpy tidak terganggu.
/// </summary>
public partial class ToolbarWindow : Window
{
    private const int HotkeyId = 0x4D52; // "MR"
    private const double ToolbarColumnDip = 52 + 8;
    private const double SideColumnDip = 196;

    private readonly ToolbarViewModel _viewModel;
    private readonly ILogger<ToolbarWindow> _logger;
    private ScrcpySession? _session;
    private nint _target;
    private nint _self;
    private HwndSource? _source;
    private WinEventHook? _systemHook;
    private WinEventHook? _objectHook;
    private WinEventHook? _foregroundHook;
    private bool _hotkeyRegistered;
    private bool _closing;
    private DockSide _side = DockSide.Right;

    public ToolbarWindow(ToolbarViewModel viewModel, ILogger<ToolbarWindow> logger)
    {
        _viewModel = viewModel;
        _logger = logger;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PickApkFile = PickApkFile;
        viewModel.GuideRequested += () => GuideRequested?.Invoke();
        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Reposition();
        Closed += OnClosed;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ToolbarViewModel.IsFilesOpen) or nameof(ToolbarViewModel.FilesPanelWidth))
            {
                Reposition();
            }
        };
    }

    /// <summary>Lebar jendela: kolom toolbar + kolom samping (panel file bila terbuka, kalau tidak lebar toast/legenda).</summary>
    private double WindowWidthDip => ToolbarColumnDip + (_viewModel.IsFilesOpen ? _viewModel.FilesPanelWidth : SideColumnDip);

    private void OnResizeDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var delta = _side == DockSide.Right ? e.HorizontalChange : -e.HorizontalChange;
        _viewModel.FilesPanelWidth = Math.Clamp(_viewModel.FilesPanelWidth + delta, ToolbarViewModel.MinFilesPanelWidth, ToolbarViewModel.MaxFilesPanelWidth);
    }

    private void OnResizeCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) => _viewModel.SaveFilesPanelWidth();

    /// <summary>Pengguna minta panduan Security settings (titik kuning / legenda); MainWindow yang menampilkan dialognya.</summary>
    public event Action? GuideRequested;

    public string Serial => _viewModel.Serial;

    /// <summary>Tunggu jendela scrcpy muncul (maks 10 detik), lalu tempelkan toolbar. Kalau jendela tidak muncul, toolbar tidak ditampilkan.</summary>
    public async Task AttachAsync(ScrcpySession session, Device device, ScrcpyOptions options)
    {
        _session = session;
        _viewModel.Initialize(session, device, options);

        var hwnd = await session.GetWindowHandleAsync();
        if (hwnd == 0 || !session.IsRunning)
        {
            _logger.LogWarning("Jendela scrcpy untuk {Name} tidak muncul; toolbar tidak ditampilkan", session.DisplayName);
            _viewModel.Dispose();
            return;
        }

        _target = hwnd;
        _viewModel.ScrcpyWindowHandle = hwnd;
        new WindowInteropHelper(this).Owner = hwnd;
        session.Exited += OnSessionExited;
        if (!session.IsRunning)
        {
            Close();
            return;
        }

        Show();
        Reposition();
        InstallHooks((uint)session.ProcessId);
        UpdateHotkey();
        _logger.LogInformation("Toolbar untuk {Name} menempel ke HWND 0x{Hwnd:X}", session.DisplayName, hwnd);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _self = new WindowInteropHelper(this).Handle;
        WindowGeometry.MakeNoActivateToolWindow(_self);
        _source = HwndSource.FromHwnd(_self);
        _source?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam == HotkeyId)
        {
            handled = true;
            if (_viewModel.ScreenshotCommand.CanExecute(null))
            {
                _viewModel.ScreenshotCommand.Execute(null);
            }
        }

        return 0;
    }

    // ---------- Mengikuti jendela scrcpy ----------

    private void InstallHooks(uint processId)
    {
        // Minimize/restore dan selesai geser/resize (0x000B..0x0017), hanya proses scrcpy.
        _systemHook = new WinEventHook(NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MINIMIZEEND, processId, OnTargetEvent);
        // Destroy/show/hide/location change (0x8001..0x800B), hanya proses scrcpy; difilter lagi ke HWND & OBJID_WINDOW.
        _objectHook = new WinEventHook(NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, processId, OnTargetEvent);
        // Foreground berubah (semua proses) → hotkey hanya saat scrcpy/toolbar aktif.
        _foregroundHook = new WinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, 0, (_, _, _) => UpdateHotkey());
        if (_systemHook.IsActive == false || _objectHook.IsActive == false)
        {
            _logger.LogWarning("SetWinEventHook gagal; toolbar tidak akan mengikuti jendela scrcpy");
        }
    }

    private void OnTargetEvent(uint eventType, nint hwnd, int idObject)
    {
        if (_closing || hwnd != _target || idObject != NativeMethods.OBJID_WINDOW)
        {
            return;
        }

        switch (eventType)
        {
            case NativeMethods.EVENT_OBJECT_DESTROY:
                SafeClose();
                break;
            case NativeMethods.EVENT_SYSTEM_MINIMIZESTART:
            case NativeMethods.EVENT_OBJECT_HIDE:
                Hide();
                break;
            case NativeMethods.EVENT_SYSTEM_MINIMIZEEND:
            case NativeMethods.EVENT_OBJECT_SHOW:
                Show();
                Reposition();
                break;
            case NativeMethods.EVENT_OBJECT_LOCATIONCHANGE:
            case NativeMethods.EVENT_SYSTEM_MOVESIZEEND:
                Reposition();
                break;
        }
    }

    /// <summary>Hitung ulang posisi/ukuran dalam piksel fisik monitor scrcpy, lalu pindahkan lewat SetWindowPos.</summary>
    private void Reposition()
    {
        if (_closing || _target == 0 || _self == 0)
        {
            return;
        }

        if (!NativeMethods.IsWindow(_target))
        {
            SafeClose();
            return;
        }

        if (NativeMethods.IsIconic(_target))
        {
            return;
        }

        var dpi = WindowGeometry.GetDpi(_target);
        var frame = WindowGeometry.GetVisibleFrame(_target);
        var client = WindowGeometry.GetClientArea(_target);
        var work = WindowGeometry.GetWorkArea(_target);

        ToolbarPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var toolbarDip = Math.Max(ToolbarPanel.DesiredSize.Height, ToolbarPanel.ActualHeight);
        var heightDip = Math.Max(toolbarDip, DockPlacement.PixelsToDip(client.Height, dpi));
        Width = WindowWidthDip;
        Height = heightDip;

        var (rect, side) = DockPlacement.Compute(
            frame,
            client.Top,
            work,
            DockPlacement.DipToPixels(WindowWidthDip, dpi),
            DockPlacement.DipToPixels(heightDip, dpi),
            DockPlacement.DipToPixels(DockPlacement.GapDip, dpi));
        WindowGeometry.MoveTo(_self, rect.Left, rect.Top);
        ApplySide(side);
    }

    /// <summary>Kolom toolbar selalu di sisi yang menempel ke scrcpy; legenda/toast di sisi luar.</summary>
    private void ApplySide(DockSide side)
    {
        if (side == _side && FirstColumn.Width.IsAbsolute == (side == DockSide.Right))
        {
            return;
        }

        _side = side;
        if (side == DockSide.Right)
        {
            FirstColumn.Width = new GridLength(52);
            SecondColumn.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(ToolbarPanel, 0);
            Grid.SetColumn(SidePanel, 1);
            SidePanel.Margin = new Thickness(8, 0, 0, 0);
            SidePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            ResizeThumb.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            FirstColumn.Width = new GridLength(1, GridUnitType.Star);
            SecondColumn.Width = new GridLength(52);
            Grid.SetColumn(ToolbarPanel, 1);
            Grid.SetColumn(SidePanel, 0);
            SidePanel.Margin = new Thickness(0, 0, 8, 0);
            SidePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            ResizeThumb.HorizontalAlignment = HorizontalAlignment.Left;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(SidePanel))
        {
            if (child is FrameworkElement fe && !ReferenceEquals(fe, FilesPanelView) && !ReferenceEquals(fe, ResizeThumb))
            {
                fe.HorizontalAlignment = side == DockSide.Right ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
        }
    }

    // ---------- Hotkey screenshot ----------

    private void UpdateHotkey()
    {
        if (_closing || _self == 0 || _target == 0)
        {
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var active = foreground == _target || foreground == _self;
        if (active && !_hotkeyRegistered)
        {
            var hotkey = _viewModel.ScreenshotHotkey;
            _hotkeyRegistered = NativeMethods.RegisterHotKey(_self, HotkeyId, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.VirtualKey);
            if (!_hotkeyRegistered)
            {
                _logger.LogWarning("Hotkey {Hotkey} tidak bisa didaftarkan (dipakai aplikasi lain?)", hotkey.Display);
            }
        }
        else if (!active && _hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(_self, HotkeyId);
            _hotkeyRegistered = false;
        }
    }

    // ---------- Lain-lain ----------

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = MoreButton;
            menu.Placement = _side == DockSide.Right ? System.Windows.Controls.Primitives.PlacementMode.Right : System.Windows.Controls.Primitives.PlacementMode.Left;
            menu.IsOpen = true;
        }
    }

    private string? PickApkFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("Apk_DialogTitle"),
            Filter = Strings.Get("Apk_Filter"),
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void OnSessionExited(ScrcpySession session, int code) => Dispatcher.BeginInvoke(SafeClose);

    private void SafeClose()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            Close();
        }
        catch (InvalidOperationException)
        {
            // Jendela sudah dihancurkan bersama owner-nya.
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closing = true;
        if (_session is not null)
        {
            _session.Exited -= OnSessionExited;
        }

        _systemHook?.Dispose();
        _objectHook?.Dispose();
        _foregroundHook?.Dispose();
        if (_hotkeyRegistered && _self != 0)
        {
            NativeMethods.UnregisterHotKey(_self, HotkeyId);
            _hotkeyRegistered = false;
        }

        _source?.RemoveHook(WndProc);
        _viewModel.Dispose();
    }
}
