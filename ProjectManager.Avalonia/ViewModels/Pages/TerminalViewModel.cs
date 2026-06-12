using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class TerminalViewModel : ViewModelBase
{
    private readonly TerminalService _terminalService;
    private readonly IProjectService _projectService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private readonly DispatcherTimer _syncTimer;

    [ObservableProperty]
    private ObservableCollection<TerminalSession> _terminalSessions = new();

    [ObservableProperty]
    private TerminalSession? _selectedSession;

    [ObservableProperty]
    private string _selectedOutput = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    private string? _pendingProjectName;
    private string? _pendingProjectPath;
    private string? _pendingStartCommand;

    public TerminalViewModel(
        TerminalService terminalService,
        IProjectService projectService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _terminalService = terminalService;
        _projectService = projectService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _syncTimer.Tick += (s, e) => SyncProjectStates();
    }

    public void OnNavigatedTo()
    {
        LoadTerminalSessions();
        _syncTimer.Start();
    }

    public void OnNavigatedFrom()
    {
        _syncTimer.Stop();
    }

    public async Task OnNavigatedToAsync()
    {
        LoadTerminalSessions();

        if (!string.IsNullOrEmpty(_pendingProjectName))
        {
            string? resolvedCommand = _pendingStartCommand;
            string? resolvedPath = _pendingProjectPath;
            try
            {
                var projects = await _projectService.GetProjectsAsync();
                var proj = projects.FirstOrDefault(p => p.Name == _pendingProjectName);
                if (string.IsNullOrWhiteSpace(resolvedCommand))
                    resolvedCommand = proj?.StartCommand;
                if (string.IsNullOrWhiteSpace(resolvedPath))
                    resolvedPath = !string.IsNullOrWhiteSpace(proj?.WorkingDirectory) ? proj!.WorkingDirectory : proj?.LocalPath;
            }
            catch { }

            var existingSession = TerminalSessions.FirstOrDefault(s => s.ProjectName == _pendingProjectName);
            if (existingSession != null)
            {
                if (!string.IsNullOrWhiteSpace(resolvedCommand))
                    existingSession.Command = resolvedCommand!;
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                    existingSession.ProjectPath = resolvedPath!;
                SelectedSession = existingSession;
            }
            else if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                var session = _terminalService.CreateSession(_pendingProjectName, resolvedPath!, resolvedCommand ?? string.Empty);
                TerminalSessions.Add(session);
                SelectedSession = session;
            }

            _pendingProjectName = null;
            _pendingProjectPath = null;
            _pendingStartCommand = null;
        }
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void LoadTerminalSessions()
    {
        IsLoading = true;
        try
        {
            var sessions = _terminalService.GetAllSessions();
            TerminalSessions.Clear();

            foreach (var session in sessions)
                TerminalSessions.Add(session);

            if (TerminalSessions.Count > 0)
                SelectedSession = TerminalSessions[0];
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StartTerminalAsync()
    {
        if (SelectedSession == null) return;

        if (string.IsNullOrWhiteSpace(SelectedSession.Command))
        {
            await _errorDisplayService.ShowErrorAsync(
                _languageService.GetString("Error_Terminal_NoStartCommand"),
                _languageService.GetString("Error_ProjectStart"));
            return;
        }

        IsLoading = true;
        try
        {
            var project = await FindProjectByNameAsync(SelectedSession.ProjectName);
            var envForLaunch = BuildEnvironmentVariables(project);
            await StartSessionWithProjectTrackingAsync(SelectedSession, project, envForLaunch);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StopTerminal()
    {
        if (SelectedSession == null) return;

        var projectName = SelectedSession.ProjectName;
        await UpdateProjectStatusAsync(projectName, SelectedSession.Process, ProjectStatus.Stopping);
        _terminalService.StopSession(SelectedSession);
        await UpdateProjectStatusAsync(projectName, null, ProjectStatus.Stopped);
    }

    [RelayCommand]
    private void ClearOutput()
    {
        SelectedSession?.ClearOutput();
    }

    [RelayCommand]
    private void CloseSession(TerminalSession? session)
    {
        if (session == null) return;

        _terminalService.RemoveSession(session.SessionId);
        TerminalSessions.Remove(session);

        if (SelectedSession == session)
            SelectedSession = TerminalSessions.FirstOrDefault();
    }

    [RelayCommand]
    private void RefreshSessions()
    {
        LoadTerminalSessions();
    }

    [RelayCommand]
    private void OpenCmd()
    {
        try
        {
            var dir = SelectedSession?.ProjectPath;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                dir = Environment.CurrentDirectory;

            var project = Task.Run(() => FindProjectByNameAsync(SelectedSession?.ProjectName)).Result;

            // Cross-platform external terminal launch
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = dir
            };

            if (OperatingSystem.IsWindows())
            {
                var initCmd = BuildCmdInitialization(dir, project);
                psi.FileName = "cmd.exe";
                psi.Arguments = string.IsNullOrEmpty(initCmd) ? "/k" : $"/k \"{initCmd}\"";
            }
            else if (OperatingSystem.IsMacOS())
            {
                psi.FileName = "open";
                psi.Arguments = "-a Terminal";
            }
            else
            {
                psi.FileName = "x-terminal-emulator";
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(
                $"{_languageService.GetString("Error_Terminal_CannotOpenCmd")}: {ex.Message}",
                _languageService.GetString("Error_ProjectStart")));
        }
    }

    private string? BuildCmdInitialization(string projectDir, Project? project)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
                return null;

            var pyPath = project?.ComfyUISettings?.PythonPath;
            if (!string.IsNullOrWhiteSpace(pyPath) && File.Exists(pyPath))
            {
                var scriptsDir = Path.GetDirectoryName(pyPath);
                if (!string.IsNullOrWhiteSpace(scriptsDir))
                {
                    var setPath = $"set \"PATH={scriptsDir};%PATH%\"";
                    var promptCmd = $"prompt ({scriptsDir}) $P$G";
                    var doskey = "doskey pip=python -m pip $*";
                    return string.Join(" && ", new List<string> { setPath, promptCmd, doskey });
                }

                var pythonParent = Path.GetDirectoryName(pyPath) ?? projectDir;
                var rootName = Path.GetFileName(pythonParent) ?? "python";
                var doskeyCmd = $"doskey pip=\"{pyPath}\" -m pip $*";
                return string.Join(" && ", new List<string> { $"prompt ({rootName}) $P$G", doskeyCmd });
            }

            var venvCandidates = new[] { ".venv", "venv", "env", ".env", "venv3", "virtualenv" };
            foreach (var name in venvCandidates)
            {
                var scriptsDir = Path.Combine(projectDir, name, "Scripts");
                var pythonExe = Path.Combine(scriptsDir, "python.exe");
                if (Directory.Exists(scriptsDir) && File.Exists(pythonExe))
                {
                    var setPath = $"set \"PATH={scriptsDir};%PATH%\"";
                    var promptCmd = $"prompt ({scriptsDir}) $P$G";
                    var doskey = "doskey pip=python -m pip $*";
                    return string.Join(" && ", new List<string> { setPath, promptCmd, doskey });
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void SetPendingProject(string projectName, string projectPath, string startCommand)
    {
        _pendingProjectName = projectName;
        _pendingProjectPath = projectPath;
        _pendingStartCommand = startCommand;
    }

    public void SetProjectPath(string projectName, string projectPath)
    {
        _pendingProjectName = projectName;
        _pendingProjectPath = projectPath;
        _pendingStartCommand = null;
    }

    public void SwitchToProjectTerminal(string projectName)
    {
        var session = TerminalSessions.FirstOrDefault(s => s.ProjectName == projectName);
        if (session != null)
            SelectedSession = session;
    }

    public async Task CreateAndStartSessionAsync(string projectName, string projectPath, string command)
    {
        var existingSession = TerminalSessions.FirstOrDefault(s => s.ProjectName == projectName);

        TerminalSession session;
        if (existingSession != null)
        {
            session = existingSession;
            session.Command = command;
            session.ProjectPath = projectPath;
            SelectedSession = session;
        }
        else
        {
            session = _terminalService.CreateSession(projectName, projectPath, command);
            TerminalSessions.Add(session);
            SelectedSession = session;
        }

        var project = await FindProjectByNameAsync(projectName);
        var envForLaunch = BuildEnvironmentVariables(project);
        await StartSessionWithProjectTrackingAsync(session, project, envForLaunch);
    }

    private void SyncProjectStates()
    {
        try
        {
            foreach (var session in TerminalSessions)
            {
                if (session.Process != null)
                {
                    if (session.Process.HasExited)
                        session.UpdateStatus(TerminalStatus.Stopped, false);
                    else
                        session.UpdateStatus(TerminalStatus.Running, true);
                }
                else
                {
                    session.UpdateStatus(TerminalStatus.Stopped, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"同步项目状态失败: {ex.Message}");
            if (ex is not TimeoutException)
            {
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(
                    $"{_languageService.GetString("Error_Terminal_SyncFailed")}: {ex.Message}",
                    _languageService.GetString("Error_ProjectStart")));
            }
        }
    }

    private async Task<Project?> FindProjectByNameAsync(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return null;

        var projects = await _projectService.GetProjectsAsync();
        return projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
    }

    private Dictionary<string, string>? BuildEnvironmentVariables(Project? project)
    {
        if (project == null) return null;

        var env = new Dictionary<string, string>(project.EnvironmentVariables ?? new Dictionary<string, string>());

        if (!string.IsNullOrWhiteSpace(project.Framework) &&
            project.Framework.Equals("ComfyUI", StringComparison.OrdinalIgnoreCase))
        {
            env["PYTHONUTF8"] = "1";
            env["PYTHONIOENCODING"] = "UTF-8";
        }

        return env;
    }

    private async Task StartSessionWithProjectTrackingAsync(TerminalSession session, Project? project, Dictionary<string, string>? environmentVariables)
    {
        string? projectName = project?.Name ?? session.ProjectName;

        if (!string.IsNullOrWhiteSpace(projectName))
            await UpdateProjectStatusAsync(projectName, null, ProjectStatus.Starting);

        var started = await _terminalService.StartSessionAsync(session, environmentVariables);

        if (string.IsNullOrWhiteSpace(projectName)) return;

        if (started)
        {
            await UpdateProjectStatusAsync(projectName, session.Process, ProjectStatus.Running);
            AttachProcessExitHandler(session, projectName);
        }
        else
        {
            await UpdateProjectStatusAsync(projectName, null, ProjectStatus.Error);
        }
    }

    private Task UpdateProjectStatusAsync(string? projectName, Process? process, ProjectStatus status)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return Task.CompletedTask;

        return _projectService.UpdateProjectRuntimeStatusAsync(projectName, process, status);
    }

    private void AttachProcessExitHandler(TerminalSession session, string projectName)
    {
        var process = session.Process;
        if (process == null) return;

        void Handler(object? sender, EventArgs args)
        {
            process.Exited -= Handler;
            _ = _projectService.UpdateProjectRuntimeStatusAsync(projectName, null, ProjectStatus.Stopped);
        }

        try { process.Exited -= Handler; } catch { }
        process.Exited += Handler;
    }
}
