using System.Collections.ObjectModel;
using System.Diagnostics;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

public interface IProjectService
{
    ObservableCollection<Project> Projects { get; }
    Task<List<Project>> GetProjectsAsync();
    Task ReloadAsync();
    Task<bool> SaveProjectAsync(Project project);
    Task SaveProjectsOrderAsync(IEnumerable<Project> orderedProjects);
    Task DeleteProjectAsync(string projectId);
    Task<Project?> GetProjectAsync(string projectId);
    Task StartProjectAsync(Project project);
    Task StopProjectAsync(Project project);
    Task<string> GetProjectLogsAsync(string projectId);
    Task<bool> UpdateProjectRuntimeStatusAsync(string projectName, Process? process, ProjectStatus status);
    Task RefreshAllProjectsGitInfoAsync();
    event EventHandler<ProjectStatusChangedEventArgs>? ProjectStatusChanged;
    event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;
}
