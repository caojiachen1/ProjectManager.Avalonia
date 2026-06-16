using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private CancellationTokenSource? _filterDebounceCts;
    private readonly TimeSpan _filterDebounceDelay = TimeSpan.FromMilliseconds(100);
    private bool _isNavigatedTo;

    [ObservableProperty]
    private ObservableCollection<Project> _filteredProjects = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedStatusFilter;

    [ObservableProperty]
    private List<string> _statusFilters = new();

    [ObservableProperty]
    private bool _hasProjects = true;

    [ObservableProperty]
    private bool _isRefreshing;

    public ObservableCollection<Project> Projects { get; }

    public ProjectsViewModel(
        IProjectService projectService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _errorDisplayService = errorDisplayService ?? throw new ArgumentNullException(nameof(errorDisplayService));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));

        // 在任何可能抛出异常的代码之前，先确保 Projects 集合已初始化
        Projects = _projectService.Projects ?? new ObservableCollection<Project>();

        try
        {
            UpdateStatusFilters();
            _languageService.LanguageChanged += (s, e) =>
            {
                UpdateStatusFilters();
                foreach (var project in Projects)
                {
                    project.RefreshStatus();
                    project.GitInfo?.RefreshStatus();
                }
            };

            _projectService.ProjectPropertyChanged += OnProjectPropertyChanged;

            RefreshFilter();
            HasProjects = Projects.Any();

            if (Projects is INotifyCollectionChanged notifyCollection)
                notifyCollection.CollectionChanged += OnProjectsCollectionChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProjectsViewModel constructor error: {ex}");
            // Projects 已在上方初始化，不会被替换

            // 重新尝试订阅 CollectionChanged
            if (Projects is INotifyCollectionChanged notifyCollection)
                notifyCollection.CollectionChanged += OnProjectsCollectionChanged;

            HasProjects = Projects.Any();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        DebouncedRefreshFilter();
    }

    partial void OnSelectedStatusFilterChanged(string? value)
    {
        RefreshFilter();
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
                    await Dispatcher.UIThread.InvokeAsync(RefreshFilter);
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void RefreshFilter()
    {
        if (Projects == null) return;

        try
        {
            var filtered = Projects.Where(FilterProject).ToList();

            FilteredProjects.Clear();
            foreach (var project in filtered)
                FilteredProjects.Add(project);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectsVM] RefreshFilter error: {ex.Message}");
            // 如果过滤失败，显示所有项目而非空列表
            FilteredProjects.Clear();
            foreach (var project in Projects)
                FilteredProjects.Add(project);
        }
    }

    private bool FilterProject(Project project)
    {
        if (project == null) return false;

        if (!string.IsNullOrEmpty(SearchText))
        {
            var name = project.Name ?? string.Empty;
            var desc = project.Description ?? string.Empty;
            var fw = project.Framework ?? string.Empty;

            var hasMatch = name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                           desc.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                           fw.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            if (!hasMatch) return false;
        }

        if (!string.IsNullOrEmpty(SelectedStatusFilter) && SelectedStatusFilter != _languageService.GetString("Filter_All"))
        {
            if (SelectedStatusFilter == _languageService.GetString("Status_Running") && project.Status != ProjectStatus.Running)
                return false;
            if (SelectedStatusFilter == _languageService.GetString("Status_Stopped") && project.Status != ProjectStatus.Stopped)
                return false;
            if (SelectedStatusFilter == _languageService.GetString("Status_Error") && project.Status != ProjectStatus.Error)
                return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task ManageComfyUIPlugins(Project project)
    {
        if (project == null) return;

        if (string.IsNullOrWhiteSpace(project.Framework) ||
            !project.Framework.Equals("ComfyUI", StringComparison.OrdinalIgnoreCase))
        {
            await _errorDisplayService.ShowWarningAsync("插件管理仅适用于 ComfyUI 类型的项目。", "不支持的项目类型");
            return;
        }

        string? customNodesPath = null;
        var comfySettings = project.ComfyUISettings;
        if (comfySettings != null && !string.IsNullOrWhiteSpace(comfySettings.ComfyUIRootPath))
        {
            var root = comfySettings.ComfyUIRootPath;
            if (Directory.Exists(root))
                customNodesPath = Path.Combine(root, "custom_nodes");
        }

        if (string.IsNullOrWhiteSpace(customNodesPath))
        {
            await _errorDisplayService.ShowErrorAsync(
                _languageService.GetString("Error_ComfyUI_InvalidRootDir"),
                _languageService.GetString("Error_ComfyUI_PathError"));
            return;
        }

        if (!Directory.Exists(customNodesPath))
        {
            var confirmed = await _errorDisplayService.ShowConfirmationAsync(
                $"未找到 custom_nodes 目录，是否为项目创建？\n\n{customNodesPath}",
                "创建插件目录");
            if (!confirmed) return;

            try
            {
                Directory.CreateDirectory(customNodesPath);
            }
            catch (Exception ex)
            {
                await _errorDisplayService.ShowErrorAsync(
                    $"{_languageService.GetString("Error_ComfyUI_CreateDirFailed")}: {ex.Message}",
                    _languageService.GetString("Error_ProjectStart"));
                return;
            }
        }

        var pluginsVm = _serviceProvider.GetRequiredService<ComfyUIPluginsManagerViewModel>();
        var window = _serviceProvider.GetRequiredService<Views.Dialogs.ComfyUIPluginsManagerWindow>();
        window.DataContext = pluginsVm;
        pluginsVm.StartLoadFromCustomNodes(customNodesPath);
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
            await window.ShowDialog(mainWindow);
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        var dialogViewModel = _serviceProvider.GetRequiredService<NewProjectDialogViewModel>();
        var window = _serviceProvider.GetRequiredService<Views.Dialogs.NewProjectWindow>();
        window.DataContext = dialogViewModel;
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var result = await window.ShowDialog<bool?>(mainWindow);
            if (result == true) await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task CloneFromGit()
    {
        var dialogViewModel = _serviceProvider.GetRequiredService<GitCloneDialogViewModel>();
        var window = _serviceProvider.GetRequiredService<Views.Dialogs.GitCloneWindow>();
        window.DataContext = dialogViewModel;
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var result = await window.ShowDialog<bool?>(mainWindow);
            if (result == true) await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task StartProject(Project project)
    {
        if (project != null)
            await _projectService.StartProjectAsync(project);
    }

    [RelayCommand]
    private async Task StopProject(Project project)
    {
        if (project != null)
            await _projectService.StopProjectAsync(project);
    }

    [RelayCommand]
    private async Task ToggleProject(Project project)
    {
        if (project != null)
        {
            if (project.Status == ProjectStatus.Running || project.Status == ProjectStatus.Starting)
                await _projectService.StopProjectAsync(project);
            else if (project.Status == ProjectStatus.Stopped || project.Status == ProjectStatus.Error)
                await _projectService.StartProjectAsync(project);
        }
    }

    [RelayCommand]
    private async Task EditProject(Project project)
    {
        if (project == null) return;

        var settingsWindowService = _serviceProvider.GetRequiredService<IProjectSettingsWindowService>();
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var result = await settingsWindowService.ShowSettingsWindowAsync(project, mainWindow);
        if (result == true)
            await LoadProjects();
    }

    [RelayCommand]
    private async Task ManageEnvironmentVariables(Project project)
    {
        if (project == null) return;

        var dialogViewModel = _serviceProvider.GetRequiredService<EnvironmentVariablesDialogViewModel>();
        dialogViewModel.LoadProject(project);

        var window = new Views.Dialogs.EnvironmentVariablesWindow(dialogViewModel);
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var result = await window.ShowDialog<bool?>(mainWindow);
        if (result == true)
        {
            await _projectService.SaveProjectAsync(project);
            await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task ManageGit(Project project)
    {
        if (project == null) return;

        if (project.GitRepositories?.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var gitService = _serviceProvider.GetRequiredService<IGitService>();
                    var projectService = _serviceProvider.GetRequiredService<IProjectService>();

                    var validationResult = await gitService.ValidateRepositoriesAsync(project.GitRepositories);

                    if (validationResult.InvalidRepositories.Count > 0)
                    {
                        project.GitRepositories = validationResult.ValidRepositories;
                        project.LastModified = DateTime.Now;
                        await projectService.SaveProjectAsync(project);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var existingProject = Projects.FirstOrDefault(p => p.Id == project.Id);
                            if (existingProject != null)
                            {
                                existingProject.GitRepositories = project.GitRepositories;
                                existingProject.LastModified = project.LastModified;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"后台Git仓库清理失败: {ex.Message}");
                }
            });
        }

        var dialogViewModel = _serviceProvider.GetRequiredService<GitManagementDialogViewModel>();
        var dialog = new Views.Dialogs.GitManagementWindow();
        dialog.DataContext = dialogViewModel;
        await dialogViewModel.LoadProjectAsync(project);
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
            await dialog.ShowDialog(mainWindow);
    }

    [RelayCommand]
    private void ViewLogs(Project project)
    {
        if (project == null) return;

        var terminalViewModel = _serviceProvider.GetService<TerminalViewModel>();
        terminalViewModel?.SetProjectPath(project.Name, project.LocalPath);

        var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainVm.NavigateToViewModel(typeof(TerminalViewModel));
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (IsRefreshing) return;

        try
        {
            IsRefreshing = true;
            await Task.Run(async () => await _projectService.ReloadAsync());

            HasProjects = Projects.Any();
            RefreshFilter();
        }
        catch (Exception ex)
        {
            await _errorDisplayService.ShowExceptionAsync(ex, "刷新项目失败");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void OpenProjectInExplorer(Project project)
    {
        if (project?.LocalPath != null && Directory.Exists(project.LocalPath))
        {
            try
            {
                ProcessInterop.OpenInFileManager(project.LocalPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open in Explorer failed: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void OpenProjectInVSCode(Project project)
    {
        if (project?.LocalPath != null && Directory.Exists(project.LocalPath))
        {
            try
            {
                Process.Start("code", $"\"{project.LocalPath}\"");
            }
            catch
            {
                _ = Task.Run(async () => await _errorDisplayService.ShowWarningAsync(
                    "无法启动 VS Code，请确保已安装 VS Code 并添加到系统路径", "错误"));
            }
        }
    }

    [RelayCommand]
    private async Task DeleteProject(Project project)
    {
        if (project == null) return;

        var confirmed = await _errorDisplayService.ShowConfirmationAsync(
            $"确定要删除项目 '{project.Name}' 吗？\n\n注意：这只会从项目管理器中移除项目记录，不会删除实际文件。",
            "确认删除");

        if (confirmed)
        {
            try
            {
                await _projectService.DeleteProjectAsync(project.Id);
                await LoadProjects();
            }
            catch (Exception ex)
            {
                await _errorDisplayService.ShowErrorAsync(
                    $"{_languageService.GetString("Error_Project_DeleteFailed")}: {ex.Message}");
            }
        }
    }

    private async Task LoadProjects()
    {
        try
        {
            // Ensure the service has loaded data (initializes from JSON file on first call)
            await _projectService.GetProjectsAsync();

            // Rebuild FilteredProjects from the shared collection
            var snapshot = (Projects ?? _projectService.Projects ?? new ObservableCollection<Project>()).ToList();
            System.Diagnostics.Debug.WriteLine($"[ProjectsVM] LoadProjects: Projects.Count={Projects?.Count ?? 0}, snapshot={snapshot.Count}");

            FilteredProjects.Clear();
            foreach (var p in snapshot.Where(FilterProject))
                FilteredProjects.Add(p);

            HasProjects = snapshot.Count > 0;
            System.Diagnostics.Debug.WriteLine($"[ProjectsVM] LoadProjects: FilteredProjects={FilteredProjects.Count}, HasProjects={HasProjects}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectsVM] LoadProjects error: {ex.Message}");
            // 确保即使出错也正确反映项目数量
            HasProjects = (Projects?.Count ?? 0) > 0;
            // 如果过滤失败，尝试直接显示所有项目
            if (FilteredProjects.Count == 0 && (Projects?.Count ?? 0) > 0)
            {
                FilteredProjects.Clear();
                foreach (var p in Projects!)
                    FilteredProjects.Add(p);
            }
        }
    }

    private void OnProjectsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            HasProjects = Projects.Any();
            RefreshFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectsVM] OnProjectsCollectionChanged error: {ex.Message}");
            HasProjects = Projects.Any();
        }
    }

    private void OnProjectPropertyChanged(object? sender, ProjectPropertyChangedEventArgs e)
    {
        if (!_isNavigatedTo) return;

        if (e.PropertyName is nameof(Project.Name) or nameof(Project.Description) or nameof(Project.Framework) or nameof(Project.Status))
        {
            DebouncedRefreshFilter();
        }
    }

    public void OnNavigatedTo()
    {
        _isNavigatedTo = true;
        _ = LoadProjects();
    }

    public void OnNavigatedFrom()
    {
        _isNavigatedTo = false;
        _filterDebounceCts?.Cancel();
    }

    public async Task OnNavigatedToAsync()
    {
        _isNavigatedTo = true;
        await LoadProjects();
        System.Diagnostics.Debug.WriteLine($"[ProjectsVM] OnNavigatedTo: HasProjects={HasProjects}, Filtered={FilteredProjects.Count}, Raw={Projects.Count}");
    }

    public async Task OnNavigatedFromAsync()
    {
        _isNavigatedTo = false;
        _filterDebounceCts?.Cancel();
        await Task.CompletedTask;
    }

    private void UpdateStatusFilters()
    {
        var all = _languageService.GetString("Filter_All");
        var running = _languageService.GetString("Status_Running");
        var stopped = _languageService.GetString("Status_Stopped");
        var error = _languageService.GetString("Status_Error");

        StatusFilters = new List<string> { all, running, stopped, error };

        if (string.IsNullOrEmpty(SelectedStatusFilter) || !StatusFilters.Contains(SelectedStatusFilter))
            SelectedStatusFilter = all;
    }

}
