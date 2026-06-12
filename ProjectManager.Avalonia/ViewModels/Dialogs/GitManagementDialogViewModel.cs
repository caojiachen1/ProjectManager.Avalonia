using System.Collections.ObjectModel;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class GitManagementDialogViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private bool _isPopulatingRepositories;

    [ObservableProperty] private Project? _project;
    [ObservableProperty] private GitInfo? _gitInfo;
    [ObservableProperty] private string _commitMessage = string.Empty;
    [ObservableProperty] private string _newBranchName = string.Empty;
    [ObservableProperty] private string _selectedBranch = string.Empty;
    [ObservableProperty] private string _remoteUrl = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<string> _availableBranches = new();
    [ObservableProperty] private ObservableCollection<GitRepositoryInfo> _availableRepositories = new();
    [ObservableProperty] private GitRepositoryInfo? _selectedRepository;
    [ObservableProperty] private bool _hasMultipleRepositories;
    [ObservableProperty] private string _currentRepositoryPath = string.Empty;

    public event EventHandler<Project>? GitInfoUpdated;

    public GitManagementDialogViewModel(
        IGitService gitService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _gitService = gitService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;
    }

    public async Task LoadProjectAsync(Project project)
    {
        Project = project;
        await LoadAvailableRepositoriesAsync();
        await RefreshGitInfoAsync();
    }

    private async Task LoadAvailableRepositoriesAsync()
    {
        if (Project == null) return;
        try
        {
            _isPopulatingRepositories = true;
            AvailableRepositories.Clear();

            var projectRootGitInfo = await _gitService.GetGitInfoAsync(Project.LocalPath);
            if (projectRootGitInfo.IsGitRepository)
                AvailableRepositories.Add(new GitRepositoryInfo(Project.LocalPath, Project.LocalPath));

            if (Project.GitRepositories?.Count > 0)
            {
                foreach (var repoPath in Project.GitRepositories)
                {
                    if (repoPath != Project.LocalPath)
                    {
                        if (await _gitService.IsValidGitRepositoryAsync(repoPath))
                            AvailableRepositories.Add(new GitRepositoryInfo(repoPath, Project.LocalPath));
                    }
                }
            }

            HasMultipleRepositories = AvailableRepositories.Count > 1;
            if (AvailableRepositories.Count > 0)
            {
                var mainRepo = AvailableRepositories.FirstOrDefault(r => r.IsMainRepository);
                SelectedRepository = mainRepo ?? AvailableRepositories.First();
                CurrentRepositoryPath = SelectedRepository.Path;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载Git仓库列表失败: {ex.Message}"); }
        finally { _isPopulatingRepositories = false; }
    }

    partial void OnSelectedRepositoryChanged(GitRepositoryInfo? value)
    {
        if (_isPopulatingRepositories || value == null || value.Path == CurrentRepositoryPath) return;
        CurrentRepositoryPath = value.Path;
        _ = RefreshGitInfoAsync();
    }

    [RelayCommand]
    private async Task RefreshGitInfo() => await RefreshGitInfoAsync();

    private string GetCurrentRepositoryPath() =>
        !string.IsNullOrEmpty(CurrentRepositoryPath) ? CurrentRepositoryPath : Project?.LocalPath ?? string.Empty;

    [RelayCommand]
    private async Task InitializeRepository()
    {
        if (Project == null) return;
        IsLoading = true;
        try
        {
            if (await _gitService.InitializeRepositoryAsync(GetCurrentRepositoryPath()))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("Git仓库初始化成功");
            }
            else await ShowErrorMessage("Git仓库初始化失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AddAllFiles()
    {
        if (Project == null || GitInfo?.IsGitRepository != true) return;
        IsLoading = true;
        try
        {
            if (await _gitService.AddAllAsync(GetCurrentRepositoryPath()))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("文件已添加到暂存区");
            }
            else await ShowErrorMessage("添加文件失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task Commit()
    {
        if (Project == null || GitInfo?.IsGitRepository != true || string.IsNullOrWhiteSpace(CommitMessage)) return;
        IsLoading = true;
        try
        {
            if (await _gitService.CommitAsync(GetCurrentRepositoryPath(), CommitMessage))
            {
                CommitMessage = string.Empty;
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("提交成功");
            }
            else await ShowErrorMessage("提交失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task Push()
    {
        if (Project == null || GitInfo?.IsGitRepository != true) return;
        IsLoading = true;
        try
        {
            if (await _gitService.PushAsync(GetCurrentRepositoryPath()))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("推送成功");
            }
            else await ShowErrorMessage("推送失败，请检查远程仓库配置");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task Pull()
    {
        if (Project == null || GitInfo?.IsGitRepository != true) return;
        IsLoading = true;
        try
        {
            if (await _gitService.PullAsync(GetCurrentRepositoryPath()))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("拉取成功");
            }
            else await ShowErrorMessage("拉取失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CreateBranch()
    {
        if (Project == null || GitInfo?.IsGitRepository != true || string.IsNullOrWhiteSpace(NewBranchName)) return;
        IsLoading = true;
        try
        {
            if (await _gitService.CreateBranchAsync(GetCurrentRepositoryPath(), NewBranchName))
            {
                NewBranchName = string.Empty;
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("分支创建成功");
            }
            else await ShowErrorMessage("分支创建失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SwitchBranch()
    {
        if (Project == null || GitInfo?.IsGitRepository != true || string.IsNullOrWhiteSpace(SelectedBranch)) return;
        IsLoading = true;
        try
        {
            if (await _gitService.SwitchBranchAsync(GetCurrentRepositoryPath(), SelectedBranch))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage($"已切换到分支: {SelectedBranch}");
            }
            else await ShowErrorMessage("分支切换失败");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SetRemoteUrl()
    {
        if (Project == null || GitInfo?.IsGitRepository != true || string.IsNullOrWhiteSpace(RemoteUrl)) return;
        IsLoading = true;
        try
        {
            if (await _gitService.SetRemoteUrlAsync(GetCurrentRepositoryPath(), RemoteUrl))
            {
                await RefreshGitInfoAsync();
                await ShowSuccessMessage("远程仓库地址设置成功");
            }
            else await ShowErrorMessage("远程仓库地址设置失败");
        }
        finally { IsLoading = false; }
    }

    private async Task RefreshGitInfoAsync()
    {
        if (Project == null) return;
        try
        {
            IsLoading = true;
            var repositoryPath = GetCurrentRepositoryPath();
            GitInfo = await _gitService.GetGitInfoAsync(repositoryPath);

            if (repositoryPath == Project.LocalPath)
                Project.GitInfo = GitInfo;

            if (GitInfo.IsGitRepository)
            {
                AvailableBranches = new ObservableCollection<string>(GitInfo.Branches);
                SelectedBranch = GitInfo.CurrentBranch;
                RemoteUrl = GitInfo.RemoteUrl;
            }

            GitInfoUpdated?.Invoke(this, Project);
        }
        finally { IsLoading = false; }
    }

    private async Task ShowSuccessMessage(string message) => await _errorDisplayService.ShowInfoAsync(message, "成功");
    private async Task ShowErrorMessage(string message) => await _errorDisplayService.ShowErrorAsync(message, _languageService.GetString("Error_ProjectStart"));
}
