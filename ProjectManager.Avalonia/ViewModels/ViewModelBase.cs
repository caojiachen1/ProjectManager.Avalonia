using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected async Task DispatchAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected async Task DispatchAsync(Func<Task> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
            await func();
        else
            await Dispatcher.UIThread.InvokeAsync(func);
    }

    protected static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    protected static TopLevel? GetTopLevel()
    {
        var mainWindow = GetMainWindow();
        return mainWindow != null ? TopLevel.GetTopLevel(mainWindow) : null;
    }

    protected async Task<string?> BrowseFolderAsync(string title, string? initialDirectory = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var options = new FolderPickerOpenOptions { Title = title };
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    protected async Task<string?> BrowseFileAsync(string title, string? initialDirectory = null, IReadOnlyList<FilePickerFileType>? fileTypeFilter = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };
        if (fileTypeFilter != null)
            options.FileTypeFilter = fileTypeFilter;
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }
}
