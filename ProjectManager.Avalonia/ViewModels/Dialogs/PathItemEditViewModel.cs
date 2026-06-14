using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class PathItemEditViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _path = string.Empty;

    public event EventHandler<bool>? CloseRequested;

    [RelayCommand]
    private async Task BrowseFolder()
    {
        try
        {
            string? initialDir = null;
            if (!string.IsNullOrEmpty(Path) && Directory.Exists(Path))
                initialDir = Path;

            var selectedPath = await BrowseFolderAsync("选择文件夹", initialDir);
            if (selectedPath != null)
            {
                Path = selectedPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"浏览文件夹时出错: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task BrowseFile()
    {
        try
        {
            string? initialDir = null;
            if (!string.IsNullOrEmpty(Path))
            {
                if (File.Exists(Path))
                    initialDir = System.IO.Path.GetDirectoryName(Path);
                else if (Directory.Exists(Path))
                    initialDir = Path;
            }

            var filePath = await BrowseFileAsync("选择文件", initialDir);
            if (filePath != null)
            {
                Path = filePath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"浏览文件时出错: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            return;
        }

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }
}
