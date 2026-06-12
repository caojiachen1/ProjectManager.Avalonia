using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services
{
    public class ProjectService : IProjectService
    {
        private readonly string _projectsFilePath;
        private readonly ObservableCollection<Project> _projects;
        private readonly TerminalService _terminalService;
        private readonly IGitService _gitService;
        private readonly IErrorDisplayService _errorDisplayService;
        private static readonly TimeSpan GitInfoCacheDuration = TimeSpan.FromMinutes(5); // 增加缓存时间
        private readonly ConcurrentDictionary<string, GitInfoCacheEntry> _gitInfoCache = new();
        private readonly ConcurrentDictionary<string, Task> _gitInfoRefreshTasks = new();
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly SemaphoreSlim _saveLock = new(1, 1); // 防止并发保存

        public event EventHandler<ProjectStatusChangedEventArgs>? ProjectStatusChanged;
        public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;

        public ObservableCollection<Project> Projects => _projects;

        public ProjectService(TerminalService terminalService, IGitService gitService, IErrorDisplayService errorDisplayService)
        {
            _terminalService = terminalService;
            _gitService = gitService;
            _errorDisplayService = errorDisplayService;
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "ProjectManager");
            Directory.CreateDirectory(appFolder);
            _projectsFilePath = Path.Combine(appFolder, "projects.json");
            _projects = new ObservableCollection<Project>();
            // 延迟加载项目，不阻塞构造函数
            _ = EnsureInitializedAsync();
        }

        /// <summary>
        /// 确保项目已初始化加载
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;
            
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;
                await LoadProjectsAsync();
                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<List<Project>> GetProjectsAsync()
        {
            await EnsureInitializedAsync();
            var snapshot = _projects.ToList();

            // 批量处理Git信息，减少单独调用次数
            var projectsNeedingRefresh = new List<Project>();
            foreach (var project in snapshot)
            {
                if (!TryApplyCachedGitInfo(project))
                {
                    projectsNeedingRefresh.Add(project);
                }
            }

            // 异步批量刷新Git信息，不阻塞主流程
            if (projectsNeedingRefresh.Count > 0)
            {
                _ = Task.Run(() => BatchRefreshGitInfoAsync(projectsNeedingRefresh));
            }

            return snapshot;
        }

        public async Task ReloadAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                await LoadProjectsAsync();
                var projects = _projects.ToList();
                // 强制刷新Git信息，忽略缓存
                foreach (var project in projects)
                {
                    InvalidateGitInfoCache(project.Id);
                }
                await BatchRefreshGitInfoAsync(projects);
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task RefreshAllProjectsGitInfoAsync()
        {
            await EnsureInitializedAsync();
            var projects = _projects.ToList();
            // 强制刷新，忽略缓存
            foreach (var project in projects)
            {
                InvalidateGitInfoCache(project.Id);
            }
            await BatchRefreshGitInfoAsync(projects);
        }

        /// <summary>
        /// 批量刷新Git信息
        /// </summary>
        private async Task BatchRefreshGitInfoAsync(List<Project> projects)
        {
            // 限制并发数量以避免过多Git进程
            var semaphore = new SemaphoreSlim(3);
            var tasks = projects.Select(async project =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await RefreshGitInfoCoreAsync(project);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        public async Task<bool> SaveProjectAsync(Project project)
        {
            await EnsureInitializedAsync();

            // Safety: ensure every project has a unique ID
            if (string.IsNullOrWhiteSpace(project.Id))
                project.Id = Guid.NewGuid().ToString();
            
            // 检查项目名称是否已存在（不包括当前项目本身）
            var existingProjectWithSameName = _projects.FirstOrDefault(p => 
                p.Name.Equals(project.Name, StringComparison.OrdinalIgnoreCase) && p.Id != project.Id);
            
            if (existingProjectWithSameName != null)
            {
                await _errorDisplayService.ShowWarningAsync(
                    $"项目名称 '{project.Name}' 已存在，请使用不同的名称。",
                    "项目名称冲突");
                return false;
            }

            var existingProject = _projects.FirstOrDefault(p => p.Id == project.Id);
            if (existingProject != null)
            {
                if (!ReferenceEquals(existingProject, project))
                {
                    CopyProjectValues(existingProject, project);
                }
                existingProject.LastModified = DateTime.Now;
            }
            else
            {
                project.LastModified = DateTime.Now;
                AttachProject(project);
                _projects.Add(project);
            }
            await SaveProjectsToFile();
            
            // 只有在项目路径变更时才刷新Git信息
            var needsGitRefresh = existingProject == null || 
                !string.Equals(existingProject.LocalPath, project.LocalPath, StringComparison.OrdinalIgnoreCase);
            
            if (needsGitRefresh)
            {
                InvalidateGitInfoCache(project.Id);
                // 异步刷新，不阻塞保存
                _ = Task.Run(() => RefreshGitInfoCoreAsync(project));
            }
            
            return true;
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            await EnsureInitializedAsync();
            
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project != null)
            {
                if (project.Status == ProjectStatus.Running)
                {
                    await StopProjectAsync(project);
                }
                DetachProject(project);
                _projects.Remove(project);
                InvalidateGitInfoCache(project.Id);
                await SaveProjectsToFile();
            }
        }

        public async Task<Project?> GetProjectAsync(string projectId)
        {
            await EnsureInitializedAsync();
            return _projects.FirstOrDefault(p => p.Id == projectId);
        }

        public Task<bool> UpdateProjectRuntimeStatusAsync(string projectName, Process? process, ProjectStatus status)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return Task.FromResult(false);

            var project = _projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
            if (project == null)
                return Task.FromResult(false);

            var statusChanged = project.Status != status;
            var processChanged = project.RunningProcess != process;

            if (!statusChanged && !processChanged)
                return Task.FromResult(false);

            project.RunningProcess = process;
            project.Status = status;

            return Task.FromResult(true);
        }

        public async Task StartProjectAsync(Project project)
        {
            if (project.Status == ProjectStatus.Running)
                return;

            try
            {
                project.Status = ProjectStatus.Starting;

                if (string.IsNullOrWhiteSpace(project.StartCommand))
                {
                    project.Status = ProjectStatus.Stopped;
                    project.RunningProcess = null;
                    project.LogOutput += $"[{DateTime.Now:HH:mm:ss}] 未配置启动命令，已自动停止\n";
                    _ = await SaveProjectAsync(project);
                    return;
                }

                // 创建或获取终端会话
                var existingSessions = _terminalService.GetAllSessions();
                var existingSession = existingSessions.FirstOrDefault(s => s.ProjectName == project.Name);
                
                TerminalSession terminalSession;
                if (existingSession == null)
                {
                    // 创建新的终端会话
                    terminalSession = _terminalService.CreateSession(
                        project.Name, 
                        string.IsNullOrEmpty(project.WorkingDirectory) ? project.LocalPath : project.WorkingDirectory,
                        project.StartCommand,
                        project.EnvironmentVariables);
                }
                else
                {
                    // 使用现有会话，但更新启动命令和工作目录
                    terminalSession = existingSession;
                    terminalSession.Command = project.StartCommand;
                    terminalSession.ProjectPath = string.IsNullOrEmpty(project.WorkingDirectory) ? project.LocalPath : project.WorkingDirectory;
                }

                // 启动终端会话，传递环境变量
                // 若为 ComfyUI 项目，则自动注入 Python UTF-8（对其他项目不注入，不做任何提示）
                Dictionary<string, string>? envForLaunch = project.EnvironmentVariables;
                if (!string.IsNullOrWhiteSpace(project.Framework) && project.Framework.Equals("ComfyUI", StringComparison.OrdinalIgnoreCase))
                {
                    envForLaunch = new Dictionary<string, string>(project.EnvironmentVariables ?? new Dictionary<string, string>())
                    {
                        ["PYTHONUTF8"] = "1",
                        ["PYTHONIOENCODING"] = "UTF-8"
                    };
                }
                var sessionStarted = await _terminalService.StartSessionAsync(terminalSession, envForLaunch);
                
                if (sessionStarted)
                {
                    // 如果终端会话启动成功，同步项目状态
                    project.RunningProcess = terminalSession.Process;
                    project.ProcessId = terminalSession.Process?.Id;
                    project.Status = ProjectStatus.Running;
                    
                    // 监听进程退出事件
                    if (terminalSession.Process != null)
                    {
                        try 
                        {
                            terminalSession.Process.EnableRaisingEvents = true;
                        }
                        catch { /* Ignore if already exited */ }

                        terminalSession.Process.Exited += (sender, e) =>
                        {
                            project.Status = ProjectStatus.Stopped;
                            project.RunningProcess = null;
                            project.ProcessId = null;
                        };
                    }
                }
                else
                {
                    project.Status = ProjectStatus.Error;
                    project.LogOutput += $"[{DateTime.Now:HH:mm:ss}] 终端会话启动失败\n";
                }

                _ = await SaveProjectAsync(project);
            }
            catch (Exception ex)
            {
                project.Status = ProjectStatus.Error;
                project.LogOutput += $"[{DateTime.Now:HH:mm:ss}] 启动失败: {ex.Message}\n";
                
                // 显示错误消息
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync($"项目启动失败: {ex.Message}", "项目启动错误"));
            }
        }

        public async Task StopProjectAsync(Project project)
        {
            // 允许停止的情况：状态为运行中，或者有残留的进程对象/ID
            if (project.Status != ProjectStatus.Running && project.RunningProcess == null && project.ProcessId == null)
                return;

            try
            {
                project.Status = ProjectStatus.Stopping;

                // 同时停止终端会话
                var existingSessions = _terminalService.GetAllSessions();
                var terminalSession = existingSessions.FirstOrDefault(s => s.ProjectName == project.Name);
                if (terminalSession != null)
                {
                    _terminalService.StopSession(terminalSession);
                }

                // 尝试停止进程对象
                if (project.RunningProcess != null)
                {
                    if (!project.RunningProcess.HasExited)
                    {
                        try
                        {
                            project.RunningProcess.Kill(true);
                        }
                        catch (Exception)
                        {
                            // 忽略无法终止已退出进程的错误
                        }
                    }
                }
                // 如果进程对象为空但有PID（例如重启后），尝试通过PID终止
                else if (project.ProcessId.HasValue)
                {
                    try
                    {
                        var p = Process.GetProcessById(project.ProcessId.Value);
                        p.Kill(true);
                    }
                    catch (Exception)
                    {
                        // 忽略找不到进程或无法访问的错误
                    }
                }

                project.RunningProcess = null;
                project.ProcessId = null;
                project.Status = ProjectStatus.Stopped;
                
                _ = await SaveProjectAsync(project);
            }
            catch (Exception ex)
            {
                // 如果是由于进程已退出导致的异常，不应视为错误
                if (project.RunningProcess == null && project.ProcessId == null)
                {
                    project.Status = ProjectStatus.Stopped;
                }
                else
                {
                    project.Status = ProjectStatus.Error;
                    project.LogOutput += $"[{DateTime.Now:HH:mm:ss}] 停止失败: {ex.Message}\n";
                    
                    // 显示错误消息
                    _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync($"项目停止失败: {ex.Message}", "项目停止错误"));
                }
            }
        }

        public async Task<string> GetProjectLogsAsync(string projectId)
        {
            var project = await GetProjectAsync(projectId);
            return project?.LogOutput ?? string.Empty;
        }

        public async Task SaveProjectsOrderAsync(IEnumerable<Project> orderedProjects)
        {
            await EnsureInitializedAsync();
            
            if (orderedProjects == null) return;

            // Rebuild internal list order based on provided sequence of project IDs
            var orderedList = orderedProjects.Select(p => p.Id).ToList();
            var newList = new List<Project>();
            foreach (var id in orderedList)
            {
                var existing = _projects.FirstOrDefault(p => p.Id == id);
                if (existing != null)
                    newList.Add(existing);
            }

            // Append any missing projects that were not included in the ordered list
            foreach (var p in _projects)
            {
                if (!newList.Any(np => np.Id == p.Id))
                    newList.Add(p);
            }

            _projects.Clear();
            foreach (var project in newList)
            {
                AttachProject(project);
                _projects.Add(project);
            }

            await SaveProjectsToFile();
        }

        /// <summary>
        /// 异步加载项目列表
        /// </summary>
        private async Task LoadProjectsAsync()
        {
            try
            {
                if (!File.Exists(_projectsFilePath)) return;

                var json = await File.ReadAllTextAsync(_projectsFilePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var dtoList = JsonSerializer.Deserialize<List<PersistedProject>>(json, options);
                if (dtoList == null) return;

                await RunOnUiThreadAsync(() =>
                {
                    foreach (var existing in _projects.ToList())
                    {
                        DetachProject(existing);
                    }
                    _projects.Clear();

                    foreach (var dto in dtoList)
                    {
                        try
                        {
                            var model = ProjectPersistenceMapper.ToModel(dto);

                            // 尝试恢复运行状态
                            if (model.Status == ProjectStatus.Running && model.ProcessId.HasValue)
                            {
                                try
                                {
                                    var process = Process.GetProcessById(model.ProcessId.Value);
                                    if (process != null && !process.HasExited)
                                    {
                                        model.RunningProcess = process;
                                        try { process.EnableRaisingEvents = true; } catch { }
                                        process.Exited += (s, e) =>
                                        {
                                            model.Status = ProjectStatus.Stopped;
                                            model.RunningProcess = null;
                                            model.ProcessId = null;
                                        };
                                    }
                                    else
                                    {
                                        model.Status = ProjectStatus.Stopped;
                                        model.ProcessId = null;
                                    }
                                }
                                catch
                                {
                                    model.Status = ProjectStatus.Stopped;
                                    model.ProcessId = null;
                                }
                            }
                            else if (model.Status != ProjectStatus.Stopped)
                            {
                                model.Status = ProjectStatus.Stopped;
                            }

                            AttachProject(model);
                            _projects.Add(model);
                        }
                        catch (Exception mapEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"映射项目失败 (ID={dto.Id}): {mapEx.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载项目失败: {ex.Message}");
                // 显示加载错误
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync($"加载项目失败: {ex.Message}", "项目加载错误"));
            }
        }

        private async Task SaveProjectsToFile()
        {
            // 使用信号量防止并发保存
            await _saveLock.WaitAsync();
            try
            {
                var dtoList = _projects.Select(ProjectPersistenceMapper.ToDto).ToList();
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                var json = JsonSerializer.Serialize(dtoList, options);
                
                // 使用临时文件写入，然后原子替换，避免文件损坏
                var tempPath = _projectsFilePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);
                
                // 删除旧文件并重命名临时文件
                if (File.Exists(_projectsFilePath))
                {
                    File.Delete(_projectsFilePath);
                }
                File.Move(tempPath, _projectsFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存项目失败: {ex.Message}");
                // 显示保存错误
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync($"保存项目失败: {ex.Message}", "项目保存错误"));
            }
            finally
            {
                _saveLock.Release();
            }
        }
        /// <summary>
        /// 尝试应用缓存的Git信息
        /// </summary>
        private bool TryApplyCachedGitInfo(Project project)
        {
            if (string.IsNullOrWhiteSpace(project?.LocalPath))
                return true; // 没有路径的项目不需要Git信息
            
            if (_gitInfoCache.TryGetValue(project.Id, out var entry))
            {
                if (DateTime.UtcNow - entry.Timestamp <= GitInfoCacheDuration)
                {
                    project.GitInfo = entry.Info;
                    return true;
                }

                _gitInfoCache.TryRemove(project.Id, out _);
            }

            return false;
        }

        private async Task RefreshGitInfoCoreAsync(Project project)
        {
            try
            {
                var gitInfo = await _gitService.GetGitInfoAsync(project.LocalPath);
                _gitInfoCache[project.Id] = new GitInfoCacheEntry(gitInfo, DateTime.UtcNow);
                await RunOnUiThreadAsync(() => project.GitInfo = gitInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新Git信息失败: {ex.Message}");
            }
            finally
            {
                _gitInfoRefreshTasks.TryRemove(project.Id, out _);
            }
        }

        private void InvalidateGitInfoCache(string projectId)
        {
            _gitInfoCache.TryRemove(projectId, out _);
            _gitInfoRefreshTasks.TryRemove(projectId, out _);
        }

        private void AttachProject(Project project)
        {
            if (project == null)
                return;

            project.PropertyChanged -= OnProjectPropertyChanged;
            project.PropertyChanged += OnProjectPropertyChanged;
        }

        private void DetachProject(Project project)
        {
            if (project == null)
                return;

            project.PropertyChanged -= OnProjectPropertyChanged;
        }

        private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not Project project || string.IsNullOrEmpty(e.PropertyName))
                return;

            ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(project, e.PropertyName));

            if (e.PropertyName == nameof(Project.Status))
            {
                ProjectStatusChanged?.Invoke(this, new ProjectStatusChangedEventArgs(project.Id, project.Status));
            }
        }

        private static void CopyProjectValues(Project target, Project source)
        {
            if (target == null || source == null)
                return;

            target.Name = source.Name;
            target.Description = source.Description;
            target.LocalPath = source.LocalPath;
            target.StartCommand = source.StartCommand;
            target.WorkingDirectory = source.WorkingDirectory;
            target.Framework = source.Framework;
            target.GitInfo = source.GitInfo;
            target.CreatedDate = source.CreatedDate;
            target.LastModified = source.LastModified;
            target.Status = source.Status;
            target.RunningProcess = source.RunningProcess;
            target.LogOutput = source.LogOutput;
            target.Tags = source.Tags != null ? new List<string>(source.Tags) : new List<string>();
            target.AutoStart = source.AutoStart;
            target.EnvironmentVariables = source.EnvironmentVariables != null
                ? new Dictionary<string, string>(source.EnvironmentVariables)
                : new Dictionary<string, string>();
            target.GitRepositories = source.GitRepositories != null
                ? new List<string>(source.GitRepositories)
                : new List<string>();
            target.ComfyUISettings = source.ComfyUISettings;
            target.NodeJSSettings = source.NodeJSSettings;
            target.DotNetSettings = source.DotNetSettings;
        }

        private static async Task RunOnUiThreadAsync(Action updateAction)
        {
            if (updateAction == null)
                return;

            if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                updateAction();
                return;
            }

            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(updateAction);
        }

        private readonly record struct GitInfoCacheEntry(GitInfo Info, DateTime Timestamp);
    }
}
