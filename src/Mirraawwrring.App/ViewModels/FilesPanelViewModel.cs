using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mirraawwrring.App.Resources;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Files;
using Mirraawwrring.Core.Settings;

namespace Mirraawwrring.App.ViewModels;

public enum FileKind
{
    Folder,
    Image,
    Video,
    Other,
}

/// <summary>Satu baris di daftar file HP.</summary>
public sealed partial class RemoteEntryViewModel(RemoteEntry entry) : ObservableObject
{
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".bmp" };
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".3gp", ".webm", ".avi" };

    public RemoteEntry Entry { get; } = entry;

    public string Name => Entry.Name;

    public string Path => Entry.Path;

    public bool IsDirectory => Entry.IsDirectory;

    public string SizeText => IsDirectory ? string.Empty : SizeFormat.Format(Entry.Size);

    public string ModifiedText => Entry.Modified.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);

    public FileKind Kind => IsDirectory ? FileKind.Folder
        : ImageExt.Contains(System.IO.Path.GetExtension(Name)) ? FileKind.Image
        : VideoExt.Contains(System.IO.Path.GetExtension(Name)) ? FileKind.Video
        : FileKind.Other;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed record CrumbViewModel(string Name, string Path, bool IsLast);

public sealed partial class ShortcutViewModel(string name, string path) : ObservableObject
{
    public string Name { get; } = name;

    public string Path { get; } = path;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

public enum EditorMode
{
    None,
    NewFolder,
    Rename,
}

/// <summary>Format ukuran file: B / KB / MB / GB mengikuti budaya aktif (mis. "3,2 MB").</summary>
public static class SizeFormat
{
    public static string Format(long bytes)
    {
        var culture = CultureInfo.CurrentCulture;
        return bytes switch
        {
            < 1024 => Strings.Format("Size_Bytes", bytes),
            < 1024 * 1024 => Strings.Format("Size_KB", (bytes / 1024.0).ToString("0", culture)),
            < 1024L * 1024 * 1024 => Strings.Format("Size_MB", (bytes / (1024.0 * 1024)).ToString("0.#", culture)),
            _ => Strings.Format("Size_GB", (bytes / (1024.0 * 1024 * 1024)).ToString("0.##", culture)),
        };
    }
}

/// <summary>
/// ViewModel panel file browser (mockup Running-Files, §5.9). Semua operasi lewat <see cref="RemoteFileService"/>;
/// hapus selalu lewat <see cref="Confirm"/> (PROMPT.md §9). Dialog (pilih file/folder, konfirmasi) disediakan View lewat delegate.
/// </summary>
public sealed partial class FilesPanelViewModel : ObservableObject, IDisposable
{
    private readonly RemoteFileService _files;
    private readonly DeviceActions _actions;
    private readonly ISettingsStore _settings;
    private readonly ILogger<FilesPanelViewModel> _logger;
    private readonly List<RemoteEntryViewModel> _all = [];
    private string _serial = string.Empty;
    private CancellationTokenSource? _transferCts;
    private CancellationTokenSource? _listCts;
    private bool _loadedOnce;

    public FilesPanelViewModel(RemoteFileService files, DeviceActions actions, ISettingsStore settings, ILogger<FilesPanelViewModel> logger)
    {
        _files = files;
        _actions = actions;
        _settings = settings;
        _logger = logger;
        foreach (var (name, path) in RemotePath.Shortcuts)
        {
            Shortcuts.Add(new ShortcutViewModel(name, path));
        }

        PullDirectory = _settings.Current.PullDirectory ?? _files.DefaultPullDirectory;
        UpdateBreadcrumb();
        UpdateSelection();
    }

    /// <summary>Konfirmasi ya/tidak (pesan, judul). Disediakan View.</summary>
    public Func<string, string, bool>? Confirm { get; set; }

    /// <summary>Dialog pilih banyak file dari PC. Disediakan View.</summary>
    public Func<string[]?>? PickFiles { get; set; }

    /// <summary>Dialog pilih folder tujuan di PC. Disediakan View.</summary>
    public Func<string, string?>? PickFolder { get; set; }

    public event Action<ToastItem>? ToastRequested;

    public event Action? CloseRequested;

    public void Initialize(string serial, string deviceName)
    {
        _serial = serial;
        DeviceName = deviceName;
    }

    /// <summary>Muat folder root saat panel pertama kali dibuka.</summary>
    public Task EnsureLoadedAsync()
    {
        if (_loadedOnce)
        {
            return Task.CompletedTask;
        }

        _loadedOnce = true;
        return NavigateAsync(CurrentPath);
    }

    // ---------- Lokasi ----------

    [ObservableProperty]
    public partial string DeviceName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParent))]
    [NotifyPropertyChangedFor(nameof(UpText))]
    public partial string CurrentPath { get; set; } = RemotePath.Root;

    public bool HasParent => RemotePath.ParentOf(CurrentPath) is not null;

    public string UpText => RemotePath.ParentOf(CurrentPath) is { } parent ? Strings.Format("Files_Up", parent == RemotePath.Root ? Strings.Get("Files_Storage") : RemotePath.NameOf(parent)) : string.Empty;

    public ObservableCollection<CrumbViewModel> Breadcrumb { get; } = [];

    public ObservableCollection<ShortcutViewModel> Shortcuts { get; } = [];

    private void UpdateBreadcrumb()
    {
        Breadcrumb.Clear();
        var crumbs = RemotePath.Breadcrumb(CurrentPath);
        for (var i = 0; i < crumbs.Count; i++)
        {
            var (name, path) = crumbs[i];
            Breadcrumb.Add(new CrumbViewModel(path == RemotePath.Root ? Strings.Get("Files_Storage") : name, path, i == crumbs.Count - 1));
        }

        foreach (var s in Shortcuts)
        {
            s.IsActive = CurrentPath == s.Path || CurrentPath.StartsWith(s.Path + "/", StringComparison.Ordinal);
        }
    }

    // ---------- Daftar ----------

    public ObservableCollection<RemoteEntryViewModel> VisibleEntries { get; } = [];

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    partial void OnFilterChanged(string value) => ApplyFilter();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Teks di tengah daftar saat kosong/gagal; null = daftar normal.</summary>
    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [RelayCommand]
    private async Task NavigateAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || _serial.Length == 0)
        {
            return;
        }

        _listCts?.Cancel();
        var cts = _listCts = new CancellationTokenSource();
        CurrentPath = RemotePath.Normalize(path);
        UpdateBreadcrumb();
        CancelEditor();
        IsLoading = true;
        ErrorText = null;
        EmptyText = Strings.Get("Files_Loading");
        try
        {
            var entries = await _files.ListAsync(_serial, CurrentPath, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            SetEntries(entries);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is RemoteFileException or InvalidOperationException or Core.Tools.ToolNotFoundException)
        {
            _logger.LogWarning("Listing {Path} gagal: {Message}", CurrentPath, ex.Message);
            SetEntries([]);
            ErrorText = Strings.Format("Files_ListFailed", ex.Message);
            EmptyText = ErrorText;
        }
        finally
        {
            if (ReferenceEquals(cts, _listCts))
            {
                IsLoading = false;
            }
        }
    }

    private void SetEntries(IReadOnlyList<RemoteEntry> entries)
    {
        foreach (var old in _all)
        {
            old.PropertyChanged -= OnEntryChanged;
        }

        _all.Clear();
        foreach (var e in entries)
        {
            var vm = new RemoteEntryViewModel(e);
            vm.PropertyChanged += OnEntryChanged;
            _all.Add(vm);
        }

        ApplyFilter();
        UpdateSelection();
    }

    private void OnEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RemoteEntryViewModel.IsSelected))
        {
            UpdateSelection();
        }
    }

    /// <summary>Filter pencarian lokal (tanpa perintah ke HP).</summary>
    private void ApplyFilter()
    {
        VisibleEntries.Clear();
        var filter = Filter.Trim();
        foreach (var e in _all)
        {
            if (filter.Length == 0 || e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                VisibleEntries.Add(e);
            }
        }

        EmptyText = _all.Count == 0 ? (ErrorText ?? Strings.Get("Files_Empty")) : VisibleEntries.Count == 0 ? Strings.Format("Files_Filtered", 0, _all.Count) : null;
        CountText = filter.Length > 0 ? Strings.Format("Files_Filtered", VisibleEntries.Count, _all.Count) : string.Empty;
    }

    [RelayCommand]
    private Task UpAsync() => NavigateAsync(RemotePath.ParentOf(CurrentPath));

    [RelayCommand]
    private Task RefreshAsync() => NavigateAsync(CurrentPath);

    /// <summary>Klik ganda: folder dibuka, file dipilih.</summary>
    [RelayCommand]
    private Task OpenAsync(RemoteEntryViewModel? entry)
    {
        if (entry is null)
        {
            return Task.CompletedTask;
        }

        if (entry.IsDirectory)
        {
            return NavigateAsync(entry.Path);
        }

        entry.IsSelected = !entry.IsSelected;
        return Task.CompletedTask;
    }

    // ---------- Pilihan ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanRename))]
    [NotifyPropertyChangedFor(nameof(PullText))]
    [NotifyCanExecuteChangedFor(nameof(PullCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(BeginRenameCommand))]
    public partial int SelectedCount { get; set; }

    public bool HasSelection => SelectedCount > 0;

    public bool CanRename => SelectedCount == 1;

    public string PullText => SelectedCount > 0 ? Strings.Format("Files_PullCount", SelectedCount) : Strings.Get("Files_Pull");

    private IReadOnlyList<RemoteEntryViewModel> Selected => _all.Where(e => e.IsSelected).ToList();

    private void UpdateSelection() => SelectedCount = _all.Count(e => e.IsSelected);

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var e in VisibleEntries)
        {
            e.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var e in _all)
        {
            e.IsSelected = false;
        }
    }

    // ---------- Editor nama (folder baru / ganti nama) ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorOpen))]
    public partial EditorMode EditorMode { get; set; }

    public bool IsEditorOpen => EditorMode != EditorMode.None;

    [ObservableProperty]
    public partial string EditorPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? EditorError { get; set; }

    [RelayCommand]
    private void BeginNewFolder()
    {
        EditorMode = EditorMode.NewFolder;
        EditorPrompt = Strings.Get("Files_NewFolderPrompt");
        EditorText = string.Empty;
        EditorError = null;
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private void BeginRename()
    {
        var target = Selected.FirstOrDefault();
        if (target is null)
        {
            return;
        }

        EditorMode = EditorMode.Rename;
        EditorPrompt = Strings.Get("Files_RenamePrompt");
        EditorText = target.Name;
        EditorError = null;
    }

    [RelayCommand]
    private void CancelEditor()
    {
        EditorMode = EditorMode.None;
        EditorError = null;
    }

    [RelayCommand]
    private async Task CommitEditorAsync()
    {
        var name = EditorText.Trim();
        if (!RemotePath.IsValidName(name))
        {
            EditorError = Strings.Get("Files_InvalidName");
            return;
        }

        try
        {
            var result = EditorMode switch
            {
                EditorMode.NewFolder => await _files.CreateDirectoryAsync(_serial, CurrentPath, name),
                EditorMode.Rename when Selected.FirstOrDefault() is { } target => await _files.RenameAsync(_serial, target.Path, name),
                _ => null,
            };
            if (result is null)
            {
                CancelEditor();
                return;
            }

            if (!result.Succeeded)
            {
                EditorError = Strings.Format("Files_CommandFailed", result.CombinedOutput);
                return;
            }

            CancelEditor();
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Editor nama gagal");
            EditorError = Strings.Format("Files_CommandFailed", ex.Message);
        }
    }

    // ---------- Hapus ----------

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var targets = Selected;
        if (targets.Count == 0 || Confirm is null)
        {
            return;
        }

        var preview = string.Join('\n', targets.Take(6).Select(t => "• " + t.Name)) + (targets.Count > 6 ? "\n…" : string.Empty);
        if (!Confirm(Strings.Format("Files_DeleteConfirm", targets.Count, preview), Strings.Get("Files_DeleteTitle")))
        {
            return;
        }

        try
        {
            var result = await _files.DeleteAsync(_serial, targets.Select(t => t.Path).ToList());
            if (!result.Succeeded)
            {
                ShowToast(Strings.Get("Files_Delete"), Strings.Format("Files_CommandFailed", result.CombinedOutput), error: true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hapus gagal");
            ShowToast(Strings.Get("Files_Delete"), ex.Message, error: true);
        }

        await RefreshAsync();
    }

    // ---------- Transfer ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullCommand))]
    [NotifyCanExecuteChangedFor(nameof(PushCommand))]
    public partial bool IsTransferring { get; set; }

    [ObservableProperty]
    public partial string TransferTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TransferDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double TransferFraction { get; set; }

    [ObservableProperty]
    public partial string TransferPercentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TransferIndeterminate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PullDirectoryText))]
    public partial string PullDirectory { get; set; } = string.Empty;

    public string PullDirectoryText => Strings.Format("Files_PullDir", ShortFolder(PullDirectory));

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    private bool CanPull() => HasSelection && !IsTransferring;

    [RelayCommand(CanExecute = nameof(CanPull))]
    private async Task PullAsync()
    {
        var targets = Selected.Select(t => t.Entry).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await RunTransferAsync(
            Strings.Format("Files_Pulling", targets.Count, ShortFolder(PullDirectory)),
            ct => _files.PullAsync(_serial, targets, PullDirectory, Progress(), ct),
            r => ShowToast(Strings.Format("Files_PullDone", r.Succeeded), ShortFolder(PullDirectory), error: false, folder: PullDirectory,
                file: r.Succeeded == 1 && targets.Count == 1 && !targets[0].IsDirectory ? Path.Combine(PullDirectory, targets[0].Name) : null));
    }

    private bool CanPush() => !IsTransferring;

    [RelayCommand(CanExecute = nameof(CanPush))]
    private async Task PushAsync()
    {
        var picked = PickFiles?.Invoke();
        if (picked is null || picked.Length == 0)
        {
            return;
        }

        await PushPathsAsync(picked);
    }

    private async Task PushPathsAsync(IReadOnlyList<string> localPaths)
    {
        var target = CurrentPath;
        await RunTransferAsync(
            Strings.Format("Files_Pushing", localPaths.Count, RemotePath.NameOf(target) == "sdcard" ? Strings.Get("Files_Storage") : RemotePath.NameOf(target)),
            ct => _files.PushAsync(_serial, localPaths, target, Progress(), ct),
            r => ShowToast(Strings.Format("Files_PushDone", r.Succeeded), target, error: false));
        await RefreshAsync();
    }

    private async Task RunTransferAsync(string title, Func<CancellationToken, Task<TransferResult>> run, Action<TransferResult> onSuccess)
    {
        if (IsTransferring)
        {
            return;
        }

        _transferCts = new CancellationTokenSource();
        IsTransferring = true;
        TransferTitle = title;
        TransferDetail = string.Empty;
        TransferFraction = 0;
        TransferPercentText = "0%";
        TransferIndeterminate = false;
        try
        {
            var result = await run(_transferCts.Token);
            if (result.Cancelled)
            {
                StatusText = Strings.Get("Files_TransferCancelled");
                ShowToast(Strings.Get("Files_TransferCancelled"), string.Empty, error: true);
            }
            else if (result.Failures.Count > 0)
            {
                var first = result.Failures[0];
                StatusText = Strings.Format("Files_TransferFailed", first.Name, first.Message);
                ShowToast(Strings.Format("Files_TransferFailed", result.Failures.Count, first.Message), first.Name, error: true);
            }
            else
            {
                StatusText = string.Empty;
                onSuccess(result);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = Strings.Get("Files_TransferCancelled");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Transfer gagal");
            StatusText = Strings.Format("Files_CommandFailed", ex.Message);
            ShowToast(Strings.Get("Toast_ActionFailed"), ex.Message, error: true);
        }
        finally
        {
            IsTransferring = false;
            _transferCts?.Dispose();
            _transferCts = null;
        }
    }

    private IProgress<TransferProgress> Progress() => new Progress<TransferProgress>(p =>
    {
        TransferFraction = p.Fraction;
        TransferPercentText = ((int)Math.Round(p.Fraction * 100)).ToString(CultureInfo.CurrentCulture) + "%";
        TransferIndeterminate = p.TotalBytes == 0 && p.FileIndex < p.FileCount;
        TransferDetail = p.CurrentName.Length > 0 ? Strings.Format("Files_TransferFile", p.CurrentName, p.FileIndex + 1, p.FileCount) : string.Empty;
    });

    [RelayCommand]
    private void CancelTransfer() => _transferCts?.Cancel();

    [RelayCommand]
    private void ChangePullDirectory()
    {
        var picked = PickFolder?.Invoke(PullDirectory);
        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        PullDirectory = picked;
        _settings.Update(s => s.PullDirectory = picked);
    }

    /// <summary>Drag & drop dari Explorer: .apk ditawarkan untuk di-install, sisanya dikirim ke folder ini.</summary>
    [RelayCommand]
    private async Task DropFilesAsync(string[]? paths)
    {
        if (paths is null || paths.Length == 0 || IsTransferring)
        {
            return;
        }

        var toPush = new List<string>();
        foreach (var path in paths)
        {
            if (Path.GetExtension(path).Equals(".apk", StringComparison.OrdinalIgnoreCase) && Confirm is not null
                && Confirm(Strings.Format("Files_ApkConfirm", Path.GetFileName(path)), Strings.Get("Files_ApkTitle")))
            {
                await InstallApkAsync(path);
            }
            else
            {
                toPush.Add(path);
            }
        }

        if (toPush.Count > 0)
        {
            await PushPathsAsync(toPush);
        }
    }

    private async Task InstallApkAsync(string path)
    {
        ShowToast(Strings.Get("Toast_ApkInstalling"), Path.GetFileName(path), error: false);
        try
        {
            var result = await _actions.InstallApkAsync(_serial, path);
            if (result.Succeeded && !result.CombinedOutput.Contains("Failure", StringComparison.Ordinal))
            {
                ShowToast(Strings.Get("Toast_ApkInstalled"), Path.GetFileName(path), error: false);
            }
            else
            {
                ShowToast(Strings.Get("Toast_ApkFailed"), result.CombinedOutput.Length > 0 ? result.CombinedOutput.Split('\n')[0] : $"exit {result.ExitCode}", error: true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Install APK gagal");
            ShowToast(Strings.Get("Toast_ApkFailed"), ex.Message, error: true);
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private void ShowToast(string title, string body, bool error, string? file = null, string? folder = null) =>
        ToastRequested?.Invoke(new ToastItem(title, body, file, folder, error));

    private static string ShortFolder(string folder)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return folder.StartsWith(profile, StringComparison.OrdinalIgnoreCase) && folder.Length > profile.Length ? folder[(profile.Length + 1)..] : folder;
    }

    public void Dispose()
    {
        _transferCts?.Cancel();
        _listCts?.Cancel();
    }
}
