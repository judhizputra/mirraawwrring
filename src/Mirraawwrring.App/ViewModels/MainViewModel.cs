using CommunityToolkit.Mvvm.ComponentModel;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core;

namespace Mirraawwrring.App.ViewModels;

/// <summary>ViewModel jendela utama. Fase 0: hanya menampilkan info fondasi; diisi penuh pada Fase 2.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(IAppPaths paths)
    {
        DataLocation = Strings.Format("Main_DataLocation", paths.DataDirectory);
    }

    [ObservableProperty]
    public partial string DataLocation { get; set; }
}
