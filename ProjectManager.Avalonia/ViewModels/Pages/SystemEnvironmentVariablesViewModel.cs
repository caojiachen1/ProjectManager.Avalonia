using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class SystemEnvironmentVariablesViewModel : ViewModelBase
{
    private readonly EnvironmentVariableService _envService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private bool _isUpdatingSelection;
    private bool _isInitialized;
    private CancellationTokenSource? _filterDebounceCts;
    private readonly TimeSpan _filterDebounceDelay = TimeSpan.FromMilliseconds(100);

    [ObservableProperty]
    private ObservableCollection<SystemEnvironmentVariable> _userVariables = new();

    [ObservableProperty]
    private ObservableCollection<SystemEnvironmentVariable> _systemVariables = new();

    [ObservableProperty]
    private SystemEnvironmentVariable? _selectedUserVariable;

    [ObservableProperty]
    private SystemEnvironmentVariable? _selectedSystemVariable;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedFilterIndex;

    [ObservableProperty]
    private int _userVariablesCount;

    [ObservableProperty]
    private int _systemVariablesCount;

    [ObservableProperty]
    private ObservableCollection<SystemEnvironmentVariable> _filteredUserVariables = new();

    [ObservableProperty]
    private ObservableCollection<SystemEnvironmentVariable> _filteredSystemVariables = new();

    [ObservableProperty]
    private bool _isLoading;

    public bool HasUserVariables => UserVariables.Count > 0;
    public bool HasSystemVariables => SystemVariables.Count > 0;
    public bool HasSelectedUserVariable => SelectedUserVariable != null;
    public bool HasSelectedSystemVariable => SelectedSystemVariable != null;
    public bool HasSelection => SelectedUserVariable != null || SelectedSystemVariable != null;

    public SystemEnvironmentVariablesViewModel(
        EnvironmentVariableService envService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _envService = envService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        RefreshUserFilter();
        RefreshSystemFilter();
    }

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        await LoadEnvironmentVariablesAsync();
        _isInitialized = true;
    }

    private async Task LoadEnvironmentVariablesAsync()
    {
        IsLoading = true;
        try
        {
            var (userVarsList, systemVarsList) = await Task.Run(() =>
            {
                var userVars = new List<SystemEnvironmentVariable>();
                var systemVars = new List<SystemEnvironmentVariable>();

                var userEnvVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
                foreach (System.Collections.DictionaryEntry entry in userEnvVars)
                {
                    userVars.Add(new SystemEnvironmentVariable(
                        entry.Key.ToString() ?? string.Empty,
                        entry.Value?.ToString() ?? string.Empty,
                        false));
                }

                var sysEnvVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
                foreach (System.Collections.DictionaryEntry entry in sysEnvVars)
                {
                    systemVars.Add(new SystemEnvironmentVariable(
                        entry.Key.ToString() ?? string.Empty,
                        entry.Value?.ToString() ?? string.Empty,
                        true));
                }

                userVars.Sort((a, b) =>
                {
                    int aUnderscores = a.Name.Length - a.Name.TrimStart('_').Length;
                    int bUnderscores = b.Name.Length - b.Name.TrimStart('_').Length;
                    if (aUnderscores != bUnderscores)
                        return bUnderscores.CompareTo(aUnderscores);
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                systemVars.Sort((a, b) =>
                {
                    int aUnderscores = a.Name.Length - a.Name.TrimStart('_').Length;
                    int bUnderscores = b.Name.Length - b.Name.TrimStart('_').Length;
                    if (aUnderscores != bUnderscores)
                        return bUnderscores.CompareTo(aUnderscores);
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                return (userVars, systemVars);
            });

            UserVariables.Clear();
            SystemVariables.Clear();

            foreach (var v in userVarsList) UserVariables.Add(v);
            foreach (var v in systemVarsList) SystemVariables.Add(v);

            UserVariablesCount = UserVariables.Count;
            SystemVariablesCount = SystemVariables.Count;

            OnPropertyChanged(nameof(HasUserVariables));
            OnPropertyChanged(nameof(HasSystemVariables));

            RefreshUserFilter();
            RefreshSystemFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载环境变量时出错: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshUserFilter()
    {
        var filtered = UserVariables.Where(FilterUserVariable).ToList();
        FilteredUserVariables.Clear();
        foreach (var v in filtered) FilteredUserVariables.Add(v);
    }

    private void RefreshSystemFilter()
    {
        var filtered = SystemVariables.Where(FilterSystemVariable).ToList();
        FilteredSystemVariables.Clear();
        foreach (var v in filtered) FilteredSystemVariables.Add(v);
    }

    private bool FilterUserVariable(SystemEnvironmentVariable variable)
    {
        if (variable.IsSystemVariable) return false;
        if (SelectedFilterIndex == 2) return false;

        if (!string.IsNullOrEmpty(SearchText))
        {
            return variable.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   variable.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    private bool FilterSystemVariable(SystemEnvironmentVariable variable)
    {
        if (!variable.IsSystemVariable) return false;
        if (SelectedFilterIndex == 1) return false;

        if (!string.IsNullOrEmpty(SearchText))
        {
            return variable.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   variable.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }

    partial void OnSearchTextChanged(string value) => DebouncedRefreshFilter();

    partial void OnSelectedFilterIndexChanged(int value)
    {
        RefreshUserFilter();
        RefreshSystemFilter();
    }

    private void DebouncedRefreshFilter()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_filterDebounceDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RefreshUserFilter();
                        RefreshSystemFilter();
                    });
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    partial void OnSelectedUserVariableChanged(SystemEnvironmentVariable? value)
    {
        if (_isUpdatingSelection) return;
        if (value != null && SelectedSystemVariable != null)
        {
            _isUpdatingSelection = true;
            SelectedSystemVariable = null;
            _isUpdatingSelection = false;
        }
        OnPropertyChanged(nameof(HasSelectedUserVariable));
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSelectedSystemVariableChanged(SystemEnvironmentVariable? value)
    {
        if (_isUpdatingSelection) return;
        if (value != null && SelectedUserVariable != null)
        {
            _isUpdatingSelection = true;
            SelectedUserVariable = null;
            _isUpdatingSelection = false;
        }
        OnPropertyChanged(nameof(HasSelectedSystemVariable));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private async Task DeleteUserVariable()
    {
        if (SelectedUserVariable == null) return;

        var confirmed = await _errorDisplayService.ShowConfirmationAsync(
            $"确定要删除用户环境变量 '{SelectedUserVariable.Name}' 吗？",
            "确认删除");

        if (confirmed)
        {
            if (_envService.DeleteVariable(SelectedUserVariable.Name, false))
                _ = LoadEnvironmentVariablesAsync();
            else
                await _errorDisplayService.ShowErrorAsync(_languageService.GetString("Error_EnvVar_DeleteUserFailed"));
        }
    }

    [RelayCommand]
    private async Task DeleteSystemVariable()
    {
        if (SelectedSystemVariable == null) return;

        var confirmed = await _errorDisplayService.ShowConfirmationAsync(
            $"确定要删除系统环境变量 '{SelectedSystemVariable.Name}' 吗？\n\n注意：此操作需要管理员权限。",
            "确认删除");

        if (confirmed)
            await DeleteSystemVariableWithUac(SelectedSystemVariable.Name);
    }

    private async Task DeleteSystemVariableWithUac(string name)
    {
        if (_envService.HasAdminPrivileges())
        {
            if (_envService.DeleteVariable(name, true))
                _ = LoadEnvironmentVariablesAsync();
            else
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(_languageService.GetString("Error_EnvVar_DeleteSystemFailed")));
        }
        else
        {
            if (_envService.DeleteVariable(name, true))
                _ = LoadEnvironmentVariablesAsync();
            else
                _ = Task.Run(async () => await _errorDisplayService.ShowInfoAsync("删除系统环境变量失败，用户取消了提权或权限不足。", "提示"));
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadEnvironmentVariablesAsync();
    }

    [RelayCommand]
    private async Task EditSelected()
    {
        if (SelectedUserVariable != null)
            await EditVariable(SelectedUserVariable, false);
        else if (SelectedSystemVariable != null)
            await EditVariable(SelectedSystemVariable, true);
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (SelectedUserVariable != null)
            await DeleteUserVariable();
        else if (SelectedSystemVariable != null)
            await DeleteSystemVariable();
    }

    [RelayCommand]
    private async Task AddUserVariable()
    {
        var newVariable = new SystemEnvironmentVariable("新变量", "", false);

        var viewModel = new EditEnvironmentVariableViewModel(newVariable, false, true);
        var editWindow = new Views.Dialogs.EditEnvironmentVariableWindow(viewModel);
        var mainWindow = GetMainWindow();
        if (mainWindow != null && await editWindow.ShowDialog<bool?>(mainWindow) == true)
        {
            if (_envService.SetUserVariable(viewModel.VariableName, viewModel.VariableValue))
                _ = LoadEnvironmentVariablesAsync();
            else
                await _errorDisplayService.ShowErrorAsync(_languageService.GetString("Error_EnvVar_AddUserFailed"));
        }
    }

    [RelayCommand]
    private async Task AddSystemVariable()
    {
        var newVariable = new SystemEnvironmentVariable("新变量", "", true);

        var viewModel = new EditEnvironmentVariableViewModel(newVariable, true, true);
        var editWindow = new Views.Dialogs.EditEnvironmentVariableWindow(viewModel);
        var mainWindow = GetMainWindow();
        if (mainWindow != null && await editWindow.ShowDialog<bool?>(mainWindow) == true)
            await SaveSystemVariableWithUac(viewModel.VariableName, viewModel.VariableValue);
    }

    [RelayCommand]
    private async Task EditUserVariable()
    {
        if (SelectedUserVariable == null) return;
        await EditVariable(SelectedUserVariable, false);
    }

    [RelayCommand]
    private async Task EditSystemVariable()
    {
        if (SelectedSystemVariable == null) return;
        await EditVariable(SelectedSystemVariable, true);
    }

    private async Task EditVariable(SystemEnvironmentVariable variable, bool isSystemVariable)
    {
        try
        {
            if (string.Equals(variable.Name, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                await EditPathVariable(variable, isSystemVariable);
            }
            else
            {
                await EditVariableStandard(variable, isSystemVariable);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"编辑环境变量时出错: {ex.Message}");
        }
    }

    private async Task EditPathVariable(SystemEnvironmentVariable variable, bool isSystemVariable)
    {
        var pathVm = new ViewModels.Dialogs.PathEditorViewModel(variable.Value, isSystemVariable);
        var pathWindow = new Views.Dialogs.PathEditorWindow(pathVm);
        var mainWindow = GetMainWindow();
        if (mainWindow != null && await pathWindow.ShowDialog<bool?>(mainWindow) == true)
        {
            await SaveEnvironmentVariable(variable.Name, pathVm.ResultValue, isSystemVariable);
        }
    }

    private async Task EditVariableStandard(SystemEnvironmentVariable variable, bool isSystemVariable)
    {
        var viewModel = new EditEnvironmentVariableViewModel(variable, isSystemVariable);
        var editWindow = new Views.Dialogs.EditEnvironmentVariableWindow(viewModel);
        var mainWindow = GetMainWindow();
        if (mainWindow != null && await editWindow.ShowDialog<bool?>(mainWindow) == true)
            await SaveEnvironmentVariable(viewModel.VariableName, viewModel.VariableValue, isSystemVariable);
    }

    private async Task SaveEnvironmentVariable(string name, string value, bool isSystemVariable)
    {
        if (isSystemVariable)
            await SaveSystemVariableWithUac(name, value);
        else
        {
            if (_envService.SetUserVariable(name, value))
                _ = LoadEnvironmentVariablesAsync();
            else
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(_languageService.GetString("Error_EnvVar_SaveUserFailed")));
        }
    }

    private async Task SaveSystemVariableWithUac(string name, string value)
    {
        if (_envService.HasAdminPrivileges())
        {
            if (_envService.SetSystemVariable(name, value))
                _ = LoadEnvironmentVariablesAsync();
            else
                await _errorDisplayService.ShowErrorAsync(_languageService.GetString("Error_EnvVar_SaveSystemFailed"));
        }
        else
        {
            if (_envService.SetSystemVariableWithElevation(name, value))
                _ = LoadEnvironmentVariablesAsync();
            else
                await _errorDisplayService.ShowInfoAsync("设置系统环境变量失败，用户取消了提权或权限不足。", "提示");
        }
    }

}
