using System.Windows;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.ViewModels;

namespace Mirraawwrring.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
    }
}
