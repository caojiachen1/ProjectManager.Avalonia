using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private CancellationTokenSource? _debounceCts;
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(150);
    private bool _isNavigatedTo;

    [ObservableProperty]
    private int _totalProjects;

    [ObservableProperty]
    private int _runningProjects;

    [ObservableProperty]
    private int _stoppedProjects;

    [ObservableProperty]
    private int _errorProjects;

    [ObservableProperty]
    private ObservableCollection<Project> _recentProjects = new();

    public DashboardViewModel(
        IProjectService projectService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _projectService = projectService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        _projectService.ProjectPropertyChanged += OnProjectPropertyChanged;
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        var dialogViewModel = _serviceProvider.GetRequiredService<ProjectEditDialogViewModel>();
        dialogViewModel.LoadProject();
        var window = _serviceProvider.GetRequiredService<Views.Dialogs.ProjectEditWindow>();
        window.DataContext = dialogViewModel;
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var result = await window.ShowDialog<bool?>(mainWindow);
            if (result == true) await LoadDashboardData();
        }
    }

    [RelayCommand]
    private async Task GitClone()
    {
        var dialogViewModel = _serviceProvider.GetRequiredService<GitCloneDialogViewModel>();
        var window = _serviceProvider.GetRequiredService<Views.Dialogs.GitCloneWindow>();
        window.DataContext = dialogViewModel;
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var result = await window.ShowDialog<bool?>(mainWindow);
            if (result == true) await LoadDashboardData();
        }
    }

    [RelayCommand]
    private void ViewAllProjects()
    {
        var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainVm.NavigateToViewModel(typeof(ProjectsViewModel));
    }

    [RelayCommand]
    private async Task StopAllProjects()
    {
        var projects = await _projectService.GetProjectsAsync();
        var running = projects.Where(p => p.Status == ProjectStatus.Running);

        foreach (var project in running)
        {
            await _projectService.StopProjectAsync(project);
        }

        await LoadDashboardData();
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

    private async Task LoadDashboardData()
    {
        try
        {
            await _projectService.GetProjectsAsync();
            RecalculateDashboard();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载Dashboard数据失败: {ex.Message}");
            _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(
                $"{_languageService.GetString("Error_Dashboard_LoadFailed")}: {ex.Message}",
                _languageService.GetString("Error_Dashboard_DataLoadError")));
        }
    }

    private void OnProjectPropertyChanged(object? sender, ProjectPropertyChangedEventArgs e)
    {
        if (!_isNavigatedTo) return;

        if (e.PropertyName is nameof(Project.Status) or nameof(Project.LastModified))
        {
            DebouncedRecalculateDashboard();
        }
    }

    private void DebouncedRecalculateDashboard()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(RecalculateDashboard);
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void RecalculateDashboard()
    {
        var projects = _projectService.Projects;

        TotalProjects = projects.Count;
        RunningProjects = projects.Count(p => p.Status == ProjectStatus.Running);
        StoppedProjects = projects.Count(p => p.Status == ProjectStatus.Stopped);
        ErrorProjects = projects.Count(p => p.Status == ProjectStatus.Error);

        var recent = projects
            .OrderByDescending(p => p.LastModified)
            .Take(5)
            .ToList();

        RecentProjects.Clear();
        foreach (var project in recent)
        {
            RecentProjects.Add(project);
        }
    }

    public void OnNavigatedTo()
    {
        _isNavigatedTo = true;
        _ = LoadDashboardData();
    }

    public void OnNavigatedFrom()
    {
        _isNavigatedTo = false;
        _debounceCts?.Cancel();
    }

    public async Task OnNavigatedToAsync()
    {
        _isNavigatedTo = true;
        await LoadDashboardData();
    }

    public async Task OnNavigatedFromAsync()
    {
        _isNavigatedTo = false;
        _debounceCts?.Cancel();
        await Task.CompletedTask;
    }

}
