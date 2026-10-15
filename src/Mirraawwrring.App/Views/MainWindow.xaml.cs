using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.Resources;
using Mirraawwrring.App.ViewModels;

namespace Mirraawwrring.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Func<HyperOsGuideWindow> _guideFactory;
    private readonly Func<PairWirelessWindow> _pairFactory;
    private readonly Func<ConnectIpWindow> _connectFactory;
    private readonly Func<SetupWindow> _setupFactory;
    private readonly Func<ToolbarWindow> _toolbarFactory;

    public MainWindow(MainViewModel viewModel, Func<HyperOsGuideWindow> guideFactory, Func<PairWirelessWindow> pairFactory, Func<ConnectIpWindow> connectFactory, Func<SetupWindow> setupFactory, Func<ToolbarWindow> toolbarFactory)
    {
        _viewModel = viewModel;
        _guideFactory = guideFactory;
        _pairFactory = pairFactory;
        _connectFactory = connectFactory;
        _setupFactory = setupFactory;
        _toolbarFactory = toolbarFactory;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        Loaded += OnLoaded;
        viewModel.HyperOsGuideRequested += ShowGuide;
        viewModel.MessageRequested += ShowMessage;
        viewModel.PairWirelessRequested += ShowPairWireless;
        viewModel.ConnectIpRequested += ShowConnectIp;
        viewModel.SetupRequested += OnSetupRequested;
        viewModel.ToolbarRequested += OnToolbarRequested;
        viewModel.UpdateRequested += OnUpdateRequested;
        viewModel.LogLines.CollectionChanged += OnLogChanged;
    }

    private async void ShowPairWireless()
    {
        var dialog = _pairFactory();
        dialog.Owner = this;
        var ok = dialog.ShowDialog() == true;
        await _viewModel.AfterWirelessDialogAsync(ok ? dialog.ConnectedSerial : null);
    }

    private async void ShowConnectIp()
    {
        var dialog = _connectFactory();
        dialog.Owner = this;
        var ok = dialog.ShowDialog() == true;
        await _viewModel.AfterWirelessDialogAsync(ok ? dialog.ConnectedAddress : null);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        if (!_viewModel.ToolsReady)
        {
            // scrcpy/adb tidak ditemukan: wizard setup muncul otomatis (§6.1).
            await ShowSetupAsync();
        }
    }

    private async void OnSetupRequested() => await ShowSetupAsync();

    /// <summary>Pembaruan scrcpy (§5.7): pasang otomatis hanya dengan hash resmi; kalau tidak, cukup buka halaman rilis.</summary>
    private async void OnUpdateRequested(Core.Setup.UpdateCheckResult update)
    {
        var title = Strings.Get("Update_Title");
        if (_viewModel.CanInstallUpdate)
        {
            var answer = MessageBox.Show(this, Strings.Format("Update_InstallPrompt", update.Latest.Version, update.InstalledVersion), title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes)
            {
                await _viewModel.InstallUpdateAsync();
            }
        }
        else
        {
            var answer = MessageBox.Show(this, Strings.Format("Update_OpenPrompt", update.Latest.Version, update.InstalledVersion), title, MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (answer == MessageBoxResult.OK)
            {
                _viewModel.OpenReleasePage();
            }
        }
    }

    /// <summary>Toolbar samping per sesi scrcpy; menutup sendiri saat sesi berakhir.</summary>
    private async void OnToolbarRequested(Core.Scrcpy.ScrcpySession session, DeviceItemViewModel item, Core.Scrcpy.ScrcpyOptions options)
    {
        var toolbar = _toolbarFactory();
        toolbar.GuideRequested += ShowGuide;
        await toolbar.AttachAsync(session, item.Device, options);
    }

    private async Task ShowSetupAsync()
    {
        var setup = _setupFactory();
        setup.Owner = this;
        setup.ShowDialog();
        await _viewModel.ReinitializeAsync();
        if (setup.OpenPairAfterClose && _viewModel.ToolsReady)
        {
            ShowPairWireless();
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        // Auto-scroll ke baris terbaru.
        if (LogList.Template.FindName("LogScroll", LogList) is ScrollViewer scroll)
        {
            scroll.ScrollToEnd();
        }
    }

    private void ShowGuide()
    {
        var guide = _guideFactory();
        guide.Owner = this;
        guide.ShowDialog();
    }

    private void ShowMessage(string message)
    {
        MessageBox.Show(this, message, Strings.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
