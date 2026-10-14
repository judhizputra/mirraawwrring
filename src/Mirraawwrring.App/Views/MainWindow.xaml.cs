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

    public MainWindow(MainViewModel viewModel, Func<HyperOsGuideWindow> guideFactory)
    {
        _viewModel = viewModel;
        _guideFactory = guideFactory;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        Loaded += OnLoaded;
        viewModel.HyperOsGuideRequested += ShowGuide;
        viewModel.MessageRequested += ShowMessage;
        viewModel.LogLines.CollectionChanged += OnLogChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
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
