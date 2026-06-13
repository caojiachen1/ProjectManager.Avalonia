using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class NewProjectDialogViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectSettingsWindowService _settingsWindowService;
    private readonly IGitService _gitService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _projectPath = string.Empty;

    [ObservableProperty]
    private string _projectDescription = string.Empty;

    [ObservableProperty]
    private string _selectedFramework = string.Empty;

    [ObservableProperty]
    private string _frameworkDescription = string.Empty;

    [ObservableProperty]
    private bool _canProceed;

    [ObservableProperty]
    private bool _scanForGitRepositories;

    [ObservableProperty]
    private bool _isScanningGitRepositories;

    [ObservableProperty]
    private double _gitScanProgress;

    [ObservableProperty]
    private string _gitScanStatusMessage = string.Empty;

    public ObservableCollection<string> AvailableFrameworks { get; } = new()
    {
        "ComfyUI", "Node.js", ".NET", "其他"
    };

    public event EventHandler? ProjectCreated;
    public event EventHandler? DialogCancelled;
    public Project? CreatedProject { get; private set; }

    public NewProjectDialogViewModel(
        IProjectService projectService,
        IProjectSettingsWindowService settingsWindowService,
        IGitService gitService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _projectService = projectService;
        _settingsWindowService = settingsWindowService;
        _gitService = gitService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectName) or nameof(ProjectPath) or nameof(SelectedFramework))
            UpdateCanProceed();
    }

    private void UpdateCanProceed()
    {
        CanProceed = !string.IsNullOrWhiteSpace(ProjectName) &&
                     !string.IsNullOrWhiteSpace(ProjectPath) &&
                     !string.IsNullOrWhiteSpace(SelectedFramework);
    }

    public void SelectFramework(string framework)
    {
        SelectedFramework = framework;
        var config = FrameworkConfigService.GetFrameworkConfig(framework);
        FrameworkDescription = config != null
            ? $"已选择: {framework} - {config.Description}"
            : $"已选择: {framework}";
    }

    [RelayCommand]
    private async Task BrowseProjectPath()
    {
        var path = await BrowseFolderAsync("选择项目文件夹", ProjectPath);
        if (path != null)
        {
            ProjectPath = path;
            // 始终用目录名称自动填入项目名称
            ProjectName = Path.GetFileName(path);
        }
    }

    [RelayCommand]
    private async Task Next()
    {
        try
        {
            var project = new Project
            {
                Id = Guid.NewGuid().ToString(),
                Name = ProjectName.Trim(),
                Description = ProjectDescription?.Trim() ?? string.Empty,
                LocalPath = ProjectPath.Trim(),
                WorkingDirectory = ProjectPath.Trim(),
                Framework = SelectedFramework,
                CreatedDate = DateTime.Now,
                LastModified = DateTime.Now,
                Tags = new List<string>()
            };

            if (ScanForGitRepositories)
                await ScanGitRepositoriesAsync(project);

            var saveSuccess = await _projectService.SaveProjectAsync(project);

            if (saveSuccess)
            {
                CreatedProject = project;
                ProjectCreated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _errorDisplayService.ShowErrorAsync(
                    _languageService.GetString("Error_SaveSettings"),
                    _languageService.GetString("Error_ProjectStart"));
            }
        }
        catch (Exception ex)
        {
            await _errorDisplayService.ShowErrorAsync(
                $"{_languageService.GetString("Error_Project_CreateFailed")}: {ex.Message}",
                _languageService.GetString("Error_ProjectStart"));
        }
    }

    private async Task ScanGitRepositoriesAsync(Project project)
    {
        try
        {
            IsScanningGitRepositories = true;
            GitScanProgress = 0;
            GitScanStatusMessage = "开始扫描Git仓库...";

            var gitRepositories = await _gitService.ScanForGitRepositoriesAsync(
                project.LocalPath,
                new Progress<(double Progress, string Message)>(progress =>
                {
                    GitScanProgress = progress.Progress;
                    GitScanStatusMessage = progress.Message;
                }));

            project.GitRepositories = gitRepositories;
            GitScanStatusMessage = $"扫描完成，发现 {gitRepositories.Count} 个Git仓库";
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            GitScanStatusMessage = $"扫描失败: {ex.Message}";
        }
        finally
        {
            IsScanningGitRepositories = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogCancelled?.Invoke(this, EventArgs.Empty);
    }
}
