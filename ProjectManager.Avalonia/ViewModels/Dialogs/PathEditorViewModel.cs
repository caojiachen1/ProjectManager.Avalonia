using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class PathEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> _paths = new();

    [ObservableProperty]
    private string? _selectedPath;

    [ObservableProperty]
    private string _variableName = "PATH";

    [ObservableProperty]
    private bool _isSystemVariable;

    private readonly string _originalValue;

    public bool HasSelection => SelectedPath != null;
    public bool CanMoveUp => SelectedPath != null && Paths.IndexOf(SelectedPath) > 0;
    public bool CanMoveDown => SelectedPath != null && Paths.IndexOf(SelectedPath) < Paths.Count - 1;

    public string ResultValue => string.Join(";", Paths);

    public PathEditorViewModel(string pathValue, bool isSystemVariable)
    {
        _originalValue = pathValue;
        _isSystemVariable = isSystemVariable;

        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            var entries = pathValue.Split(';', StringSplitOptions.None);
            foreach (var entry in entries)
            {
                var trimmed = entry.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    Paths.Add(trimmed);
            }
        }
    }

    // Parameterless constructor for designer
    public PathEditorViewModel() : this("", false) { }

    partial void OnSelectedPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    [RelayCommand]
    private async Task BrowseAddFolder()
    {
        var selectedPath = await BrowseFolderAsync("选择要添加的文件夹");
        if (selectedPath != null)
        {
            Paths.Add(selectedPath);
            SelectedPath = selectedPath;
        }
    }

    [RelayCommand]
    private void AddEmptyPath()
    {
        var newPath = "";
        Paths.Add(newPath);
        SelectedPath = newPath;
        // User should edit this path inline or via edit command
    }

    [RelayCommand]
    private void EditPath()
    {
        // Editing is done inline in the ListBox via TextBox in the DataTemplate
        // This command is a placeholder for potential future dialog-based editing
    }

    [RelayCommand]
    private void RemovePath()
    {
        if (SelectedPath == null) return;
        var index = Paths.IndexOf(SelectedPath);
        Paths.Remove(SelectedPath);
        if (Paths.Count > 0)
        {
            if (index >= Paths.Count)
                index = Paths.Count - 1;
            SelectedPath = Paths[index];
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedPath == null) return;
        var index = Paths.IndexOf(SelectedPath);
        if (index <= 0) return;
        Paths.Move(index, index - 1);
        SelectedPath = Paths[index - 1];
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedPath == null) return;
        var index = Paths.IndexOf(SelectedPath);
        if (index >= Paths.Count - 1) return;
        Paths.Move(index, index + 1);
        SelectedPath = Paths[index + 1];
    }

    [RelayCommand]
    private void Save()
    {
        // ResultValue will be read by the calling window
    }
}
