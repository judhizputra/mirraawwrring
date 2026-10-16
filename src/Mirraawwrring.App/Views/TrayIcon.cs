using System.Drawing;
using System.Windows.Forms;
using Mirraawwrring.App.Resources;

namespace Mirraawwrring.App.Views;

/// <summary>
/// Ikon area notifikasi (tray) untuk jendela utama yang diminimalkan. WPF tidak punya NotifyIcon, jadi memakai WinForms.
/// Hanya terlihat selama jendela disembunyikan; klik kiri = tampilkan lagi, klik kanan = menu Tampilkan/Keluar.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _balloonShown;

    public TrayIcon(string tooltip, Action show, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.Get("Tray_Show"), null, (_, _) => show());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.Get("Tray_Exit"), null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Text = tooltip,
            Icon = LoadAppIcon(),
            ContextMenuStrip = menu,
            Visible = false,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                show();
            }
        };
    }

    /// <summary>Tampilkan ikon; balon petunjuk hanya sekali per proses supaya tidak mengganggu.</summary>
    public void Show()
    {
        _icon.Visible = true;
        if (!_balloonShown)
        {
            _balloonShown = true;
            _icon.ShowBalloonTip(3000, Strings.Get("Tray_BalloonTitle"), Strings.Get("Tray_BalloonText"), ToolTipIcon.None);
        }
    }

    public void Hide() => _icon.Visible = false;

    private static Icon LoadAppIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"))
            ?? throw new InvalidOperationException("Assets/icon.ico tidak ditemukan di resource aplikasi.");
        using var stream = resource.Stream;
        return new Icon(stream);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
