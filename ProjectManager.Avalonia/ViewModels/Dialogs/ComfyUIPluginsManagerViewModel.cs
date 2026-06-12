using System.Collections.ObjectModel;
using Avalonia.Threading;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class ComfyUIPluginsManagerViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly IErrorDisplayService _errorService;

    private readonly Dictionary<string, CachedGitInfo> _gitCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed class CachedGitInfo
    {
        public bool IsGitRepository { get; init; }
        public string RemoteUrl { get; init; } = string.Empty;
        public string Branch { get; init; } = string.Empty;
        public string VersionId { get; init; } = string.Empty;
        public string LastCommitMessage { get; init; } = string.Empty;
    }

    [ObservableProperty]
    private string _customNodesPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ComfyUIPluginInfo> _plugins = new();

    public ComfyUIPluginsManagerViewModel(IGitService gitService, IErrorDisplayService errorService)
    {
        _gitService = gitService;
        _errorService = errorService;
    }

    public void StartLoadFromCustomNodes(string customNodesPath)
    {
        _ = LoadFromCustomNodesAsync(customNodesPath);
    }

    public async Task LoadFromCustomNodesAsync(string customNodesPath)
    {
        if (!string.Equals(CustomNodesPath, customNodesPath, StringComparison.OrdinalIgnoreCase))
            _gitCache.Clear();

        CustomNodesPath = customNodesPath;

        if (Plugins == null)
            Plugins = new ObservableCollection<ComfyUIPluginInfo>();
        else
            await Dispatcher.UIThread.InvokeAsync(() => Plugins.Clear());

        if (string.IsNullOrWhiteSpace(customNodesPath) || !Directory.Exists(customNodesPath))
            return;

        await Task.Run(() =>
        {
            try
            {
                var dirs = Directory.GetDirectories(customNodesPath);

                foreach (var dir in dirs)
                {
                    try
                    {
                        var info = new DirectoryInfo(dir);

                        if (info.Name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase)
                            || info.Name.IndexOf("pycache", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        var plugin = new ComfyUIPluginInfo
                        {
                            Name = info.Name,
                            LastUpdated = info.LastWriteTime,
                            Path = info.FullName
                        };

                        if (_gitCache.TryGetValue(info.FullName, out var cached))
                            ApplyCachedGitInfo(plugin, cached);

                        Dispatcher.UIThread.Post(() => Plugins.Add(plugin));

                        _ = LoadGitInfoAsync(plugin, info.FullName);
                    }
                    catch { }
                }
            }
            catch { }
        });
    }

    private async Task LoadGitInfoAsync(ComfyUIPluginInfo plugin, string directoryPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                ApplyCachedGitInfo(plugin, new CachedGitInfo { IsGitRepository = false, RemoteUrl = "不是git仓库" });
                return;
            }

            if (_gitCache.TryGetValue(directoryPath, out var cachedFromCache))
                ApplyCachedGitInfo(plugin, cachedFromCache);

            var isGitRepo = await _gitService.IsValidGitRepositoryAsync(directoryPath);
            if (!isGitRepo)
            {
                var cached = new CachedGitInfo { IsGitRepository = false, RemoteUrl = "不是git仓库" };
                _gitCache[directoryPath] = cached;
                ApplyCachedGitInfo(plugin, cached);
                return;
            }

            var gitInfo = await _gitService.GetGitInfoAsync(directoryPath);
            if (!gitInfo.IsGitRepository)
            {
                var cached = new CachedGitInfo { IsGitRepository = false, RemoteUrl = "不是git仓库" };
                _gitCache[directoryPath] = cached;
                ApplyCachedGitInfo(plugin, cached);
                return;
            }

            var remoteUrl = string.IsNullOrWhiteSpace(gitInfo.RemoteUrl) ? "(无远端)" : gitInfo.RemoteUrl;
            var branch = gitInfo.CurrentBranch;
            var shortHash = await _gitService.GetShortCommitHashAsync(directoryPath);
            var lastMessage = gitInfo.LastCommitMessage;

            var updatedCache = new CachedGitInfo
            {
                IsGitRepository = true,
                RemoteUrl = remoteUrl,
                Branch = branch,
                VersionId = shortHash,
                LastCommitMessage = lastMessage
            };

            _gitCache[directoryPath] = updatedCache;
            ApplyCachedGitInfo(plugin, updatedCache);
        }
        catch
        {
            var cached = new CachedGitInfo { IsGitRepository = false, RemoteUrl = "不是git仓库" };
            _gitCache[directoryPath] = cached;
            ApplyCachedGitInfo(plugin, cached);
        }
    }

    private static void ApplyCachedGitInfo(ComfyUIPluginInfo plugin, CachedGitInfo cached)
    {
        plugin.RemoteUrl = string.IsNullOrEmpty(cached.RemoteUrl) ? "" : cached.RemoteUrl;
        plugin.Branch = cached.Branch;
        plugin.VersionId = cached.VersionId;
        plugin.LastCommitMessage = cached.LastCommitMessage;
    }

    [RelayCommand]
    private Task Refresh()
    {
        if (!string.IsNullOrWhiteSpace(CustomNodesPath))
            StartLoadFromCustomNodes(CustomNodesPath);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Remove(ComfyUIPluginInfo plugin)
    {
        if (plugin == null) return;

        var confirm = await _errorService.ShowConfirmationAsync(
            $"确定要删除插件 '{plugin.Name}' 吗？此操作不可撤销。", "确认删除");
        if (!confirm) return;

        try
        {
            if (!string.IsNullOrWhiteSpace(plugin.Path) && Directory.Exists(plugin.Path))
                Directory.Delete(plugin.Path, true);

            await Dispatcher.UIThread.InvokeAsync(() => Plugins?.Remove(plugin));
        }
        catch (Exception ex)
        {
            await _errorService.ShowExceptionAsync(ex, "删除失败");
        }
    }
}
