using System.Diagnostics;
using System.Text.RegularExpressions;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services
{
    public class GitService : IGitService
    {
        private readonly ISettingsService _settingsService;
        private readonly IErrorDisplayService _errorDisplayService;
        private string? _cachedGitExe;
        private readonly SemaphoreSlim _gitCommandSemaphore = new(5); // 限制并发Git命令数

        public GitService(ISettingsService settingsService, IErrorDisplayService errorDisplayService)
        {
            _settingsService = settingsService;
            _errorDisplayService = errorDisplayService;
        }

        private async Task<string> GetGitExecutableAsync()
        {
            if (!string.IsNullOrEmpty(_cachedGitExe))
                return _cachedGitExe!;
            try
            {
                var path = await _settingsService.GetGitExecutablePathAsync();
                _cachedGitExe = string.IsNullOrWhiteSpace(path) ? "git" : path.Trim();
            }
            catch
            {
                _cachedGitExe = "git";
            }
            return _cachedGitExe!;
        }

        /// <summary>
        /// 获取项目的Git信息
        /// </summary>
    public async Task<GitInfo> GetGitInfoAsync(string projectPath)
        {
            var gitInfo = new GitInfo();

            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                return gitInfo;

            try
            {
                // 严谨判断：仅认 .git 目录为初筛，且必须通过 git 命令二次确认
                if (!IsGitRepositoryByMarkerDirectory(projectPath))
                    return gitInfo;

                var confirm = await ConfirmGitRepositoryWithGitAsync(projectPath);
                if (!confirm)
                    return gitInfo;

                gitInfo.IsGitRepository = true;

                // 获取当前分支
                gitInfo.CurrentBranch = await GetCurrentBranchAsync(projectPath);

                // 获取所有分支
                gitInfo.Branches = await GetBranchesAsync(projectPath);

                // 获取远程URL
                gitInfo.RemoteUrl = await GetRemoteUrlAsync(projectPath);

                // 获取状态信息
                await UpdateGitStatusAsync(projectPath, gitInfo);

                // 获取最后一次提交信息
                await UpdateLastCommitInfoAsync(projectPath, gitInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取Git信息失败: {ex.Message}");
                // 显示Git信息获取错误（仅在调试模式下显示，避免频繁弹窗）
                #if DEBUG
                _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync($"获取Git信息失败: {ex.Message}", "Git信息获取错误"));
                #endif
            }

            return gitInfo;
        }

        /// <summary>
        /// 初始化Git仓库
        /// </summary>
        public async Task<bool> InitializeRepositoryAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "init");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 添加所有文件到暂存区
        /// </summary>
        public async Task<bool> AddAllAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "add .");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 提交更改
        /// </summary>
        public async Task<bool> CommitAsync(string projectPath, string message)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, $"commit -m \"{message}\"");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 推送到远程仓库
        /// </summary>
        public async Task<bool> PushAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "push");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从远程仓库拉取
        /// </summary>
        public async Task<bool> PullAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "pull");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建新分支
        /// </summary>
        public async Task<bool> CreateBranchAsync(string projectPath, string branchName)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, $"checkout -b {branchName}");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 切换分支
        /// </summary>
        public async Task<bool> SwitchBranchAsync(string projectPath, string branchName)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, $"checkout {branchName}");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有分支
        /// </summary>
        public async Task<List<string>> GetBranchesAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "branch -a");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => b.Trim().TrimStart('*').Trim())
                        .Where(b => !string.IsNullOrEmpty(b))
                        .ToList();
                }
            }
            catch { }

            return new List<string>();
        }

        /// <summary>
        /// 获取远程URL
        /// </summary>
        public async Task<string> GetRemoteUrlAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "remote get-url origin");
                return result.IsSuccess ? result.Output.Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 设置远程URL
        /// </summary>
        public async Task<bool> SetRemoteUrlAsync(string projectPath, string remoteUrl)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, $"remote set-url origin {remoteUrl}");
                return result.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 克隆远程仓库到本地
        /// </summary>
        public async Task<(bool IsSuccess, string ErrorMessage)> CloneRepositoryAsync(string remoteUrl, string localPath, IProgress<string>? progress = null)
        {
            try
            {
                // 确保本地路径存在
                var parentDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // 如果目标目录已存在且不为空，返回错误
                if (Directory.Exists(localPath) && Directory.GetFileSystemEntries(localPath).Length > 0)
                {
                    return (false, "目标目录已存在且不为空");
                }

                progress?.Report("开始克隆仓库...");

                var gitExe = await GetGitExecutableAsync();
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = gitExe,
                    Arguments = $"clone --progress \"{remoteUrl}\" \"{localPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                var output = new List<string>();
                var error = new List<string>();
                var lastProgressLine = string.Empty;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.Add(e.Data);
                        progress?.Report(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Git 进度信息通常输出到 stderr
                        var line = e.Data.Trim();
                        
                        // 处理包含回车符的进度行（Git 使用 \r 来覆盖同一行）
                        if (line.Contains('\r'))
                        {
                            var parts = line.Split('\r');
                            foreach (var part in parts)
                            {
                                if (!string.IsNullOrWhiteSpace(part))
                                {
                                    lastProgressLine = part.Trim();
                                    progress?.Report(lastProgressLine);
                                }
                            }
                        }
                        else
                        {
                            error.Add(line);
                            progress?.Report(line);
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    progress?.Report("克隆完成！");
                    return (true, string.Empty);
                }
                else
                {
                    var errorMessage = string.Join("\n", error);
                    return (false, string.IsNullOrEmpty(errorMessage) ? "克隆失败" : errorMessage);
                }
            }
            catch (Exception ex)
            {
                return (false, $"克隆过程中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证Git URL是否有效
        /// </summary>
        public async Task<bool> IsValidGitUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                // 检查URL格式
                var gitUrlPatterns = new[]
                {
                    @"^https://github\.com/[\w\-\.]+/[\w\-\.]+(?:\.git)?/?$",
                    @"^git@github\.com:[\w\-\.]+/[\w\-\.]+(?:\.git)?$",
                    @"^https://gitlab\.com/[\w\-\.]+/[\w\-\.]+(?:\.git)?/?$",
                    @"^git@gitlab\.com:[\w\-\.]+/[\w\-\.]+(?:\.git)?$",
                    @"^https://bitbucket\.org/[\w\-\.]+/[\w\-\.]+(?:\.git)?/?$",
                    @"^git@bitbucket\.org:[\w\-\.]+/[\w\-\.]+(?:\.git)?$",
                    @"^https://.*\.git$",
                    @"^git@.*:.*\.git$"
                };

                var isValidFormat = gitUrlPatterns.Any(pattern => Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase));
                if (!isValidFormat)
                    return false;

                // 尝试执行 git ls-remote 来验证仓库是否可访问
                var gitExe = await GetGitExecutableAsync();
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = gitExe,
                    Arguments = $"ls-remote --heads \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从Git URL中提取仓库名称
        /// </summary>
        public Task<string> GetRepositoryNameFromUrlAsync(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return Task.FromResult(string.Empty);

                // 移除 .git 后缀
                var cleanUrl = url.EndsWith(".git") ? url[..^4] : url;
                
                // 提取最后一部分作为仓库名
                var parts = cleanUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var repoName = parts[^1];
                    // 如果是SSH格式，可能包含冒号
                    if (repoName.Contains(':'))
                    {
                        repoName = repoName.Split(':')[^1];
                    }
                    return Task.FromResult(repoName);
                }

                return Task.FromResult(string.Empty);
            }
            catch
            {
                return Task.FromResult(string.Empty);
            }
        }

        /// <summary>
        /// 检查Git仓库路径是否有效（快速版本，仅检查文件系统）
        /// </summary>
        public Task<bool> IsValidGitRepositoryAsync(string repositoryPath)
        {
            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
                return Task.FromResult(false);

            try
            {
                // 仅检查是否有.git目录，跳过git命令验证以提高性能
                return Task.FromResult(IsGitRepositoryByMarkerDirectory(repositoryPath));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 批量验证Git仓库路径的有效性（并行优化版本）
        /// </summary>
        public async Task<(List<string> ValidRepositories, List<string> InvalidRepositories)> ValidateRepositoriesAsync(IEnumerable<string> repositoryPaths)
        {
            var validRepositories = new List<string>();
            var invalidRepositories = new List<string>();

            if (repositoryPaths == null)
                return (validRepositories, invalidRepositories);

            var paths = repositoryPaths.ToList();
            if (paths.Count == 0)
                return (validRepositories, invalidRepositories);

            // 使用并行处理以提高性能
            var validationTasks = paths.Select(async repoPath =>
            {
                var isValid = await IsValidGitRepositoryAsync(repoPath);
                return new { Path = repoPath, IsValid = isValid };
            });

            var results = await Task.WhenAll(validationTasks);

            foreach (var result in results)
            {
                if (result.IsValid)
                {
                    validRepositories.Add(result.Path);
                }
                else
                {
                    invalidRepositories.Add(result.Path);
                }
            }

            return (validRepositories, invalidRepositories);
        }

        #region Private Methods

        private async Task<string> GetCurrentBranchAsync(string projectPath)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "branch --show-current");
                return result.IsSuccess ? result.Output.Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task UpdateGitStatusAsync(string projectPath, GitInfo gitInfo)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "status --porcelain");
                if (result.IsSuccess)
                {
                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    gitInfo.UncommittedChanges = lines.Length;

                    if (lines.Length == 0)
                    {
                        gitInfo.Status = GitStatus.Clean;
                    }
                    else if (lines.Any(l => l.StartsWith("UU") || l.StartsWith("AA")))
                    {
                        gitInfo.Status = GitStatus.Conflicted;
                    }
                    else if (lines.Any(l => l[0] != ' ' && l[0] != '?'))
                    {
                        gitInfo.Status = GitStatus.Staged;
                    }
                    else if (lines.Any(l => l.StartsWith("??")))
                    {
                        gitInfo.Status = GitStatus.Untracked;
                    }
                    else
                    {
                        gitInfo.Status = GitStatus.Modified;
                    }
                }

                // 获取未推送的提交数量
                var unpushedResult = await RunGitCommandAsync(projectPath, "log @{u}..HEAD --oneline");
                if (unpushedResult.IsSuccess)
                {
                    gitInfo.UnpushedCommits = unpushedResult.Output
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                }
            }
            catch { }
        }

        private async Task UpdateLastCommitInfoAsync(string projectPath, GitInfo gitInfo)
        {
            try
            {
                var result = await RunGitCommandAsync(projectPath, "log -1 --pretty=format:\"%H|%s|%an|%ad\" --date=iso");
                if (result.IsSuccess && !string.IsNullOrEmpty(result.Output))
                {
                    var parts = result.Output.Split('|');
                    if (parts.Length >= 4)
                    {
                        gitInfo.LastCommitMessage = parts[1];
                        gitInfo.LastCommitAuthor = parts[2];
                        
                        if (DateTime.TryParse(parts[3], out var commitDate))
                        {
                            gitInfo.LastCommitDate = commitDate;
                        }
                    }
                }
            }
            catch { }
        }

        public async Task<string> GetShortCommitHashAsync(string repositoryPath)
        {
            try
            {
                var result = await RunGitCommandAsync(repositoryPath, "rev-parse --short HEAD");
                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
                {
                    return result.Output.Trim();
                }
            }
            catch
            {
                // ignore and return empty
            }

            return string.Empty;
        }

    private async Task<(bool IsSuccess, string Output)> RunGitCommandAsync(string workingDirectory, string arguments)
        {
            await _gitCommandSemaphore.WaitAsync();
            try
            {
                var gitExe = await GetGitExecutableAsync();
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = gitExe,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                
                // 使用并行读取避免死锁
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // 添加超时限制
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(process.WaitForExitAsync(), timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    try { process.Kill(); } catch { }
                    return (false, "Git命令超时");
                }

                var output = await outputTask;
                var error = await errorTask;

                return (process.ExitCode == 0, string.IsNullOrEmpty(error) ? output : error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                _gitCommandSemaphore.Release();
            }
        }

        /// <summary>
        /// 仅以 .git 目录作为仓库存在的本地标记（不解析 .git 文件）。
        /// </summary>
        private static bool IsGitRepositoryByMarkerDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
            try
            {
                var dotGit = Path.Combine(path, ".git");
                return Directory.Exists(dotGit);
            }
            catch { return false; }
        }

        /// <summary>
        /// 使用 git 命令确认目录是否为仓库：git rev-parse --git-dir
        /// 仅在存在 .git 目录的前提下调用，以降低无谓开销。
        /// </summary>
        private async Task<bool> ConfirmGitRepositoryWithGitAsync(string path)
        {
            if (!IsGitRepositoryByMarkerDirectory(path)) return false;
            try
            {
                var result = await RunGitCommandAsync(path, "rev-parse --git-dir");
                return result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output);
            }
            catch { return false; }
        }

        /// <summary>
        /// 扫描指定路径及其子文件夹中的所有Git仓库
        /// </summary>
    public async Task<List<string>> ScanForGitRepositoriesAsync(string rootPath, IProgress<(double Progress, string Message)>? progress = null)
        {
            var gitRepositories = new List<string>();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return gitRepositories;
            }

            try
            {
                progress?.Report((0, "准备扫描目录..."));

                // 使用队列进行广度优先扫描，避免Directory.GetDirectories的权限问题
                var directoriesToScan = new Queue<string>();
                var scannedDirectories = new HashSet<string>();
                var totalScanned = 0;
                var skippedDueToPremissions = 0;

                directoriesToScan.Enqueue(rootPath);

                while (directoriesToScan.Count > 0)
                {
                    var currentDirectory = directoriesToScan.Dequeue();
                    
                    // 避免重复扫描
                    if (scannedDirectories.Contains(currentDirectory))
                        continue;
                        
                    scannedDirectories.Add(currentDirectory);
                    totalScanned++;

                    try
                    {
                        // 检查是否是Git仓库（严格策略：只认 .git 目录，并用 git 二次确认）
                        var isGitRepository = false;
                        try
                        {
                            if (IsGitRepositoryByMarkerDirectory(currentDirectory))
                            {
                                isGitRepository = await ConfirmGitRepositoryWithGitAsync(currentDirectory);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // 无法访问.git目录，跳过此目录
                            progress?.Report((
                                (double)totalScanned / (totalScanned + directoriesToScan.Count) * 100,
                                $"跳过无权限目录: {Path.GetFileName(currentDirectory)}"
                            ));
                            continue;
                        }

                        if (isGitRepository)
                        {
                            gitRepositories.Add(currentDirectory);
                            progress?.Report((
                                (double)totalScanned / (totalScanned + directoriesToScan.Count) * 100,
                                $"发现Git仓库: {Path.GetFileName(currentDirectory)}"
                            ));
                            // 继续扫描其子目录，以支持仓库内的嵌套仓库（例如子模块或嵌套repo）
                        }

                        // 获取子目录并加入扫描队列
                        try
                        {
                            var subDirectories = Directory.GetDirectories(currentDirectory);
                            foreach (var subDir in subDirectories)
                            {
                                // 跳过 .git 目录以避免无效遍历和性能问题
                                var name = Path.GetFileName(subDir);
                                if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                if (!scannedDirectories.Contains(subDir))
                                {
                                    directoriesToScan.Enqueue(subDir);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            skippedDueToPremissions++;
                            progress?.Report((
                                (double)totalScanned / (totalScanned + directoriesToScan.Count) * 100,
                                $"跳过无权限目录: {Path.GetFileName(currentDirectory)} (已跳过 {skippedDueToPremissions} 个)"
                            ));
                        }
                        catch (DirectoryNotFoundException)
                        {
                            // 目录不存在，可能被删除了，跳过
                            continue;
                        }
                        catch (PathTooLongException)
                        {
                            // 路径太长，跳过
                            progress?.Report((
                                (double)totalScanned / (totalScanned + directoriesToScan.Count) * 100,
                                $"跳过路径过长的目录: {Path.GetFileName(currentDirectory)}"
                            ));
                            continue;
                        }
                        catch (IOException ex)
                        {
                            // 其他IO异常，跳过
                            System.Diagnostics.Debug.WriteLine($"扫描目录 {currentDirectory} 时遇到IO错误: {ex.Message}");
                            continue;
                        }

                        // 更新进度
                        if (totalScanned % 10 == 0)
                        {
                            var progressPercent = (double)totalScanned / Math.Max(totalScanned + directoriesToScan.Count, 1) * 100;
                            progress?.Report((
                                progressPercent,
                                $"已扫描 {totalScanned} 个目录，发现 {gitRepositories.Count} 个Git仓库"
                            ));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"扫描目录 {currentDirectory} 时出错: {ex.Message}");
                        continue;
                    }

                    // 添加一些延时以避免UI冻结（对于大型目录结构）
                    if (totalScanned % 50 == 0)
                    {
                        await Task.Delay(1);
                    }
                }

                var finalMessage = $"扫描完成，发现 {gitRepositories.Count} 个Git仓库";
                if (skippedDueToPremissions > 0)
                {
                    finalMessage += $"，跳过了 {skippedDueToPremissions} 个无权限目录";
                }
                
                progress?.Report((100, finalMessage));
                return gitRepositories;
            }
            catch (Exception ex)
            {
                progress?.Report((100, $"扫描失败: {ex.Message}"));
                System.Diagnostics.Debug.WriteLine($"Git仓库扫描失败: {ex.Message}");
                return gitRepositories;
            }
        }

        #endregion
    }
}
