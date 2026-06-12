using Avalonia.Platform.Storage;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class EditEnvironmentVariableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _variableName = string.Empty;

    [ObservableProperty]
    private string _variableValue = string.Empty;

    [ObservableProperty]
    private bool _isSystemVariable;

    [ObservableProperty]
    private bool _canEditName;

    [ObservableProperty]
    private bool _isPathVariable;

    private readonly SystemEnvironmentVariable _originalVariable;

    public event EventHandler<bool>? SaveCompleted;

    public event EventHandler<string>? PathEditRequested;

    public EditEnvironmentVariableViewModel(SystemEnvironmentVariable variable, bool isSystemVariable, bool isNewVariable = false)
    {
        _originalVariable = variable;
        _variableName = variable.Name;
        _variableValue = variable.Value;
        _isSystemVariable = isSystemVariable;
        _canEditName = isNewVariable;
        _isPathVariable = string.Equals(variable.Name, "PATH", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        try
        {
            string? initialDir = null;
            if (!string.IsNullOrEmpty(VariableValue) && Directory.Exists(VariableValue))
                initialDir = VariableValue;
            else if (!string.IsNullOrEmpty(VariableValue))
            {
                var paths = VariableValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
                initialDir = paths.FirstOrDefault(Directory.Exists);
            }

            var selectedPath = await BrowseFolderAsync("选择文件夹", initialDir);
            if (selectedPath != null)
            {
                if (string.IsNullOrEmpty(VariableValue))
                {
                    VariableValue = selectedPath;
                }
                else
                {
                    var paths = VariableValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    if (!paths.Contains(selectedPath))
                        VariableValue = string.Join(";", paths.Append(selectedPath));
                }
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
            var selectedFile = await BrowseFileAsync("选择文件");
            if (selectedFile != null)
            {
                if (string.IsNullOrEmpty(VariableValue))
                {
                    VariableValue = selectedFile;
                }
                else
                {
                    var paths = VariableValue.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    if (!paths.Contains(selectedFile))
                        VariableValue = string.Join(";", paths.Append(selectedFile));
                }
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
        try
        {
            if (string.IsNullOrWhiteSpace(VariableName))
                throw new ArgumentException("变量名不能为空");
            if (string.IsNullOrWhiteSpace(VariableValue))
                throw new ArgumentException("变量值不能为空");

            _originalVariable.Name = VariableName.Trim();
            _originalVariable.Value = VariableValue.Trim();

            SaveCompleted?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存环境变量时出错: {ex.Message}");
        }
    }

    [RelayCommand]
    private void EditPath()
    {
        // Notify the window to open the PathEditorWindow
        PathEditRequested?.Invoke(this, VariableValue);
    }
}
