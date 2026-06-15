using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class PathEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<PathItem> _pathItems = new();

    [ObservableProperty]
    private PathItem? _selectedPathItem;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private string _variableName = "PATH";

    [ObservableProperty]
    private bool _isSystemVariable;

    [ObservableProperty]
    private bool _isListMode = true;

    private readonly string _originalPath;
    private bool _isInternalUpdate;

    public event EventHandler<bool>? CloseRequested;

    public bool HasSelection => SelectedPathItem != null;
    public bool CanMoveUp => SelectedPathItem != null && PathItems.IndexOf(SelectedPathItem) > 0;
    public bool CanMoveDown => SelectedPathItem != null && PathItems.IndexOf(SelectedPathItem) < PathItems.Count - 1;

    public string ResultValue => string.Join(";", PathItems.Select(p => p.Path));

    public PathEditorViewModel(string path, bool isSystemVariable)
    {
        _originalPath = path;
        _isSystemVariable = isSystemVariable;

        PathItems.CollectionChanged += OnPathItemsCollectionChanged;

        LoadPathItems(path);
        UpdateEditText();
    }

    public PathEditorViewModel() : this("", false) { }

    private void OnPathItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyMoveRelatedProperties();
    }

    private void NotifyMoveRelatedProperties()
    {
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private void LoadPathItems(string path)
    {
        PathItems.Clear();
        if (string.IsNullOrEmpty(path))
            return;

        var paths = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pathItem in paths)
        {
            PathItems.Add(new PathItem
            {
                Path = pathItem,
                Status = GetPathStatus(pathItem)
            });
        }
    }

    private PathStatus GetPathStatus(string path)
    {
        if (string.IsNullOrEmpty(path))
            return PathStatus.Invalid;

        try
        {
            if (Directory.Exists(path))
                return PathStatus.Valid;
            if (File.Exists(path))
                return PathStatus.Valid;
            return PathStatus.NotFound;
        }
        catch
        {
            return PathStatus.Invalid;
        }
    }

    private void UpdateEditText()
    {
        _isInternalUpdate = true;
        try
        {
            EditText = string.Join(";", PathItems.Select(p => p.Path));
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    [RelayCommand]
    private async Task New()
    {
        var dialog = new Views.Dialogs.PathItemEditWindow();
        var viewModel = new PathItemEditViewModel();
        dialog.DataContext = viewModel;

        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        if (await dialog.ShowDialog<bool?>(mainWindow) == true)
        {
            var newItem = new PathItem
            {
                Path = viewModel.Path,
                Status = GetPathStatus(viewModel.Path)
            };
            PathItems.Add(newItem);
            SelectedPathItem = newItem;
            UpdateEditText();
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedPathItem == null) return;

        var dialog = new Views.Dialogs.PathItemEditWindow();
        var viewModel = new PathItemEditViewModel { Path = SelectedPathItem.Path };
        dialog.DataContext = viewModel;

        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        if (await dialog.ShowDialog<bool?>(mainWindow) == true)
        {
            SelectedPathItem.Path = viewModel.Path;
            SelectedPathItem.Status = GetPathStatus(viewModel.Path);
            UpdateEditText();
        }
    }

    [RelayCommand]
    private async Task Browse()
    {
        if (SelectedPathItem == null) return;

        string? initialDir = null;
        if (!string.IsNullOrEmpty(SelectedPathItem.Path))
        {
            if (Directory.Exists(SelectedPathItem.Path))
                initialDir = SelectedPathItem.Path;
            else if (File.Exists(SelectedPathItem.Path))
                initialDir = Path.GetDirectoryName(SelectedPathItem.Path);
        }

        var filePath = await BrowseFileAsync("选择文件", initialDir);
        if (filePath != null)
        {
            SelectedPathItem.Path = filePath;
            SelectedPathItem.Status = GetPathStatus(filePath);
            UpdateEditText();
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedPathItem == null) return;

        var index = PathItems.IndexOf(SelectedPathItem);
        PathItems.Remove(SelectedPathItem);
        if (PathItems.Count > 0)
        {
            if (index >= PathItems.Count)
                index = PathItems.Count - 1;
            SelectedPathItem = PathItems[index];
        }
        UpdateEditText();
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedPathItem == null) return;

        var item = SelectedPathItem;
        var index = PathItems.IndexOf(item);
        if (index > 0)
        {
            PathItems.RemoveAt(index);
            PathItems.Insert(index - 1, item);
            SelectedPathItem = item;
            UpdateEditText();
            NotifyMoveRelatedProperties();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedPathItem == null) return;

        var item = SelectedPathItem;
        var index = PathItems.IndexOf(item);
        if (index < PathItems.Count - 1)
        {
            PathItems.RemoveAt(index);
            PathItems.Insert(index + 1, item);
            SelectedPathItem = item;
            UpdateEditText();
            NotifyMoveRelatedProperties();
        }
    }

    partial void OnEditTextChanged(string value)
    {
        if (_isInternalUpdate) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            PathItems.Clear();
            return;
        }

        var paths = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
        PathItems.Clear();
        foreach (var path in paths)
        {
            PathItems.Add(new PathItem
            {
                Path = path.Trim(),
                Status = GetPathStatus(path.Trim())
            });
        }
    }

    [RelayCommand]
    private void Save()
    {
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }

    [RelayCommand]
    private async Task ToggleEditMode()
    {
        if (IsListMode)
        {
            var textEditWindow = new Views.Dialogs.PathTextEditWindow();
            var textEditVm = new PathTextEditViewModel(GetResultPath());
            textEditWindow.DataContext = textEditVm;

            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            if (await textEditWindow.ShowDialog<bool?>(mainWindow) == true)
            {
                LoadPathItems(textEditVm.PathText);
            }
        }
    }

    partial void OnSelectedPathItemChanged(PathItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        NotifyMoveRelatedProperties();
    }

    public string GetResultPath()
    {
        return string.Join(";", PathItems.Select(p => p.Path));
    }
}

public partial class PathItem : ObservableObject
{
    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private PathStatus _status;

    public Symbol StatusIcon => Status switch
    {
        PathStatus.Valid => Symbol.CheckmarkCircle,
        PathStatus.NotFound => Symbol.Warning,
        PathStatus.Invalid => Symbol.ErrorCircle,
        _ => Symbol.QuestionCircle
    };

    public string StatusText => Status switch
    {
        PathStatus.Valid => "有效",
        PathStatus.NotFound => "未找到",
        PathStatus.Invalid => "无效",
        _ => "未知"
    };

    public IBrush StatusColor => Status switch
    {
        PathStatus.Valid => new SolidColorBrush(Color.Parse("#4CAF50")),
        PathStatus.NotFound => new SolidColorBrush(Color.Parse("#FF9800")),
        PathStatus.Invalid => new SolidColorBrush(Color.Parse("#F44336")),
        _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
    };
}

public enum PathStatus
{
    Valid,
    NotFound,
    Invalid
}
