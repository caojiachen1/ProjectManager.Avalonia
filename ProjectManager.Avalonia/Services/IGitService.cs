using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

public interface IGitService
{
    Task<GitInfo> GetGitInfoAsync(string projectPath);
    Task<bool> InitializeRepositoryAsync(string projectPath);
    Task<bool> AddAllAsync(string projectPath);
    Task<bool> CommitAsync(string projectPath, string message);
    Task<bool> PushAsync(string projectPath);
    Task<bool> PullAsync(string projectPath);
    Task<bool> CreateBranchAsync(string projectPath, string branchName);
    Task<bool> SwitchBranchAsync(string projectPath, string branchName);
    Task<List<string>> GetBranchesAsync(string projectPath);
    Task<string> GetRemoteUrlAsync(string projectPath);
    Task<bool> SetRemoteUrlAsync(string projectPath, string remoteUrl);
    Task<(bool IsSuccess, string ErrorMessage)> CloneRepositoryAsync(string remoteUrl, string localPath, IProgress<string>? progress = null);
    Task<bool> IsValidGitUrlAsync(string url);
    Task<string> GetRepositoryNameFromUrlAsync(string url);
    Task<List<string>> ScanForGitRepositoriesAsync(string rootPath, IProgress<(double Progress, string Message)>? progress = null);
    Task<bool> IsValidGitRepositoryAsync(string repositoryPath);
    Task<(List<string> ValidRepositories, List<string> InvalidRepositories)> ValidateRepositoriesAsync(IEnumerable<string> repositoryPaths);
    Task<string> GetShortCommitHashAsync(string repositoryPath);
}
