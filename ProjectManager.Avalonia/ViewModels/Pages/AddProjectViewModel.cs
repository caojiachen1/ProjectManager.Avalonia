using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class AddProjectViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorDisplayService _errorDisplayService;

    public AddProjectViewModel(
        IProjectService projectService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IErrorDisplayService errorDisplayService)
    {
        _projectService = projectService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _errorDisplayService = errorDisplayService;
    }

    [RelayCommand]
    private async Task CreateNewProject()
    {
        var dialogViewModel = _serviceProvider.GetRequiredService<NewProjectDialogViewModel>();

        var window = _serviceProvider.GetRequiredService<Views.Dialogs.NewProjectWindow>();
        window.DataContext = dialogViewModel;
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var result = await window.ShowDialog<bool?>(mainWindow);
            if (result == true) NavigateToProjects();
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
            if (result == true) NavigateToProjects();
        }
    }

    private void NavigateToProjects()
    {
        var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainVm.NavigateToViewModel(typeof(ProjectsViewModel));
    }

    public void OnNavigatedTo() { }
    public void OnNavigatedFrom() { }
    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;
}
