using System.Windows;
using Mirraawwrring.App.Interop;

namespace Mirraawwrring.App.Views;

/// <summary>Panduan mengaktifkan "USB debugging (Security settings)" di Xiaomi/HyperOS.</summary>
public partial class HyperOsGuideWindow : Window
{
    public HyperOsGuideWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
    }
}
