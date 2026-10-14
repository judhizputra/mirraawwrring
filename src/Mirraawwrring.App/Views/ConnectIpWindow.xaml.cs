using System.Windows;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.ViewModels;

namespace Mirraawwrring.App.Views;

public partial class ConnectIpWindow : Window
{
    private readonly ConnectIpViewModel _viewModel;

    public ConnectIpWindow(ConnectIpViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        viewModel.Completed += () => Dispatcher.InvokeAsync(() => DialogResult = true);
        Loaded += (_, _) =>
        {
            AddressBox.Focus();
            AddressBox.SelectAll();
        };
    }

    public string? ConnectedAddress => _viewModel.ConnectedAddress;
}
