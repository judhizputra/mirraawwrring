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

    public MainWindow(MainViewModel viewModel, Func<HyperOsGuideWindow> guideFactory, Func<PairWirelessWindow> pairFactory, Func<ConnectIpWindow> connectFactory, Func<SetupWindow> setupFactory)
    {
        _viewModel = viewModel;
        _guideFactory = guideFactory;
        _pairFactory = pairFactory;
        _connectFactory = connectFactory;
        _setupFactory = setupFactory;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        Loaded += OnLoaded;
        viewModel.HyperOsGuideRequested += ShowGuide;
        viewModel.MessageRequested += ShowMessage;
        viewModel.PairWirelessRequested += ShowPairWireless;
        viewModel.ConnectIpRequested += ShowConnectIp;
        viewModel.SetupRequested += OnSetupRequested;
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
