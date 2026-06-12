using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> GetSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
        Task<string> GetGitUserNameAsync();
        Task<string> GetGitUserEmailAsync();
        Task<string> GetGitExecutablePathAsync();
        Task SetGitUserNameAsync(string userName);
        Task SetGitUserEmailAsync(string userEmail);
        Task SetGitExecutablePathAsync(string gitPath);
        Task<string> GetDefaultProjectPathAsync();
        Task SetDefaultProjectPathAsync(string path);
        Task<bool> GetAutoStartProjectsAsync();
        Task SetAutoStartProjectsAsync(bool autoStart);
    }
}
