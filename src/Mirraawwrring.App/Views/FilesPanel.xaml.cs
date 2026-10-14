using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mirraawwrring.App.Resources;
using Mirraawwrring.App.ViewModels;

namespace Mirraawwrring.App.Views;

/// <summary>Panel file browser (UserControl di dalam ToolbarWindow). Dialog Windows (pilih file/folder, konfirmasi) dipasang ke ViewModel di sini.</summary>
public partial class FilesPanel : UserControl
{
    public FilesPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => SetDragHighlight(false);
        Drop += OnDrop;
    }

    private FilesPanelViewModel? ViewModel => DataContext as FilesPanelViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FilesPanelViewModel old)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is not FilesPanelViewModel vm)
        {
            return;
        }

        vm.Confirm = (message, title) => MessageBox.Show(Window.GetWindow(this), message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        vm.PickFiles = PickFiles;
        vm.PickFolder = PickFolder;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilesPanelViewModel.IsEditorOpen) && ViewModel?.IsEditorOpen == true)
        {
            Dispatcher.BeginInvoke(() =>
            {
                EditorBox.Focus();
                EditorBox.SelectAll();
            });
        }
    }

    private string[]? PickFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("Files_PickPush"),
            Filter = Strings.Get("Files_AllFiles"),
            Multiselect = true,
            CheckFileExists = true,
        };
        return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FileNames : null;
    }

    private string? PickFolder(string initial)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.Get("Files_PickPullDir"),
            InitialDirectory = System.IO.Directory.Exists(initial) ? initial : null,
        };
        return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FolderName : null;
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindRow(source) is { DataContext: RemoteEntryViewModel entry } && ViewModel is { } vm)
        {
            vm.OpenCommand.Execute(entry);
            e.Handled = true;
        }
    }

    private static ListBoxItem? FindRow(DependencyObject source)
    {
        var current = source;
        while (current is not null and not ListBoxItem)
        {
            current = VisualTreeHelper.GetParent(current);
        }

        return current as ListBoxItem;
    }

    // ---------- Drag & drop dari Explorer ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop) && ViewModel is { IsTransferring: false };
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragHighlight(ok);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        SetDragHighlight(false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && ViewModel is { } vm)
        {
            e.Handled = true;
            _ = vm.DropFilesCommand.ExecuteAsync(paths);
        }
    }

    private void SetDragHighlight(bool on)
    {
        DropOutline.Stroke = (Brush)FindResource(on ? "Brush.Accent" : "Brush.BorderControl");
    }
}
