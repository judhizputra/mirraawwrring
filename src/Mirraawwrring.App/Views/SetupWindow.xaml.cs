using System.Windows;
using Microsoft.Win32;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.Resources;
using Mirraawwrring.App.ViewModels.Setup;

namespace Mirraawwrring.App.Views;

/// <summary>Jendela wizard setup (mockup Setup1..Setup4). Semua logika di <see cref="SetupViewModel"/>; code-behind hanya dialog folder dan panduan.</summary>
public partial class SetupWindow : Window
{
    private readonly SetupViewModel _viewModel;
    private readonly Func<HyperOsGuideWindow> _guideFactory;

    public SetupWindow(SetupViewModel viewModel, Func<HyperOsGuideWindow> guideFactory)
    {
        _viewModel = viewModel;
        _guideFactory = guideFactory;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        Loaded += async (_, _) => await viewModel.StartAsync();
        Closed += (_, _) => viewModel.Dispose();
        viewModel.CloseRequested += completed => Dispatcher.InvokeAsync(() =>
        {
            Completed = completed;
            DialogResult = completed;
        });
        viewModel.OpenPairRequested += () => OpenPairAfterClose = true;
        viewModel.OpenHyperOsGuideRequested += () =>
        {
            var guide = _guideFactory();
            guide.Owner = this;
            guide.ShowDialog();
        };
    }

    /// <summary>True jika wizard ditutup lewat "Buka Mirraawwrring" dengan tools lengkap.</summary>
    public bool Completed { get; private set; }

    /// <summary>True jika pengguna memilih "Tanpa kabel"/"Pakai Wi-Fi saja": pemanggil membuka dialog Pair.</summary>
    public bool OpenPairAfterClose { get; private set; }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Strings.Get("Install_PickFolder"), Multiselect = false };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Install.SetFolder(dialog.FolderName);
        }
    }
}
