using System.Collections.ObjectModel;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class ProjectEditDialogViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private Project? _originalProject;
    private bool _isLoadingProject;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _projectDescription = string.Empty;

    [ObservableProperty]
    private string _localPath = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _startCommand = string.Empty;

    [ObservableProperty]
    private string _framework = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _availableFrameworks = new();

    [ObservableProperty]
    private ObservableCollection<string> _frameworkCommands = new();

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private string _tagsString = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    public event EventHandler<Project>? ProjectSaved;
    public event EventHandler<string>? ProjectDeleted;
    public event EventHandler? DialogCancelled;

    public ProjectEditDialogViewModel(
        IProjectService projectService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _projectService = projectService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        AvailableFrameworks = new ObservableCollection<string>(FrameworkConfigService.GetFrameworkNames());
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Framework))
            OnFrameworkChanged();
    }

    private void OnFrameworkChanged()
    {
        if (string.IsNullOrEmpty(Framework)) return;

        var config = FrameworkConfigService.GetFrameworkConfig(Framework);
        if (config != null)
        {
            FrameworkCommands = new ObservableCollection<string>(config.CommonCommands);

            if (string.IsNullOrEmpty(StartCommand) && !_isLoadingProject && !IsEditing)
                StartCommand = config.DefaultStartCommand;

            if (string.IsNullOrEmpty(TagsString))
                TagsString = string.Join(", ", config.DefaultTags);
        }
    }

    public void LoadProject(Project? project = null)
    {
        _originalProject = project;
        _isLoadingProject = true;

        if (project != null)
        {
            IsEditing = true;
            ProjectName = project.Name ?? string.Empty;
            ProjectDescription = project.Description ?? string.Empty;
            LocalPath = project.LocalPath ?? string.Empty;
            WorkingDirectory = project.WorkingDirectory ?? string.Empty;
            StartCommand = project.StartCommand ?? string.Empty;
            Framework = project.Framework ?? string.Empty;
            AutoStart = project.AutoStart;
            TagsString = project.Tags != null ? string.Join(", ", project.Tags) : string.Empty;
        }
        else
        {
            IsEditing = false;
            ProjectName = string.Empty;
            ProjectDescription = string.Empty;
            LocalPath = string.Empty;
            WorkingDirectory = string.Empty;
            StartCommand = string.Empty;
            Framework = string.Empty;
            AutoStart = false;
            TagsString = string.Empty;
            FrameworkCommands.Clear();
        }
        _isLoadingProject = false;
    }

    [RelayCommand]
    private async Task BrowseLocalPath()
    {
        var path = await BrowseFolderAsync("选择项目文件夹", LocalPath);
        if (path != null)
        {
            LocalPath = path;
            if (string.IsNullOrEmpty(WorkingDirectory))
                WorkingDirectory = path;
            if (string.IsNullOrEmpty(ProjectName))
                ProjectName = Path.GetFileName(path);
        }
    }

    [RelayCommand]
    private void ApplyFrameworkCommand(string command)
    {
        if (!string.IsNullOrEmpty(command))
            StartCommand = command;
    }

    [RelayCommand]
    private async Task BrowseWorkingDirectory()
    {
        var path = await BrowseFolderAsync("选择工作目录", WorkingDirectory);
        if (path != null)
            WorkingDirectory = path;
    }

    [RelayCommand]
    private async Task Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                await ShowErrorMessage("项目名称不能为空");
                return;
            }
            if (string.IsNullOrWhiteSpace(LocalPath))
            {
                await ShowErrorMessage("项目路径不能为空");
                return;
            }

            var project = _originalProject ?? new Project
            {
                Id = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.Now
            };
            project.Name = ProjectName.Trim();
            project.Description = ProjectDescription?.Trim() ?? string.Empty;
            project.LocalPath = LocalPath.Trim();
            project.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? LocalPath.Trim() : WorkingDirectory.Trim();
            project.StartCommand = StartCommand?.Trim() ?? string.Empty;
            project.Framework = Framework?.Trim() ?? string.Empty;
            project.AutoStart = AutoStart;

            if (!string.IsNullOrWhiteSpace(TagsString))
            {
                project.Tags = TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }
            else
            {
                project.Tags = new List<string>();
            }

            project.LastModified = DateTime.Now;
            if (_originalProject == null)
                project.CreatedDate = DateTime.Now;

            var saveSuccess = await _projectService.SaveProjectAsync(project);
            if (saveSuccess)
                ProjectSaved?.Invoke(this, project);
        }
        catch (Exception ex)
        {
            await ShowErrorMessage($"保存项目失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel() => DialogCancelled?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task Delete()
    {
        if (_originalProject != null)
        {
            try
            {
                var confirm = await _errorDisplayService.ShowConfirmationAsync(
                    $"确定要删除项目 '{_originalProject.Name}' 吗？\n此操作不可撤销。", "确认删除");
                if (confirm)
                {
                    await _projectService.DeleteProjectAsync(_originalProject.Id);
                    ProjectDeleted?.Invoke(this, _originalProject.Id);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"删除项目失败: {ex.Message}");
            }
        }
    }

    private async Task ShowErrorMessage(string message)
    {
        await _errorDisplayService.ShowErrorAsync(message, _languageService.GetString("Error_ProjectStart"));
    }
}
