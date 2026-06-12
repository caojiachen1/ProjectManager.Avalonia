using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// 表示一个 ComfyUI 插件（custom_nodes 下的一个子目录）。
    /// </summary>
    public partial class ComfyUIPluginInfo : ObservableObject
    {
        [ObservableProperty]
        private bool _isEnabled = true;

        /// <summary>
        /// 插件目录名，例如 "ComfyUI-QualityOfLifeSuit_Omar92"。
        /// </summary>
        [ObservableProperty]
        private string _name = string.Empty;

        /// <summary>
        /// 远端地址（如果能从文件推断出来），例如 Git 仓库 URL。
        /// </summary>
        [ObservableProperty]
        private string _remoteUrl = string.Empty;

        /// <summary>
        /// 当前分支名称（可选）。
        /// </summary>
        [ObservableProperty]
        private string _branch = string.Empty;

        /// <summary>
        /// 版本 ID（例如提交哈希）。
        /// </summary>
        [ObservableProperty]
        private string _versionId = string.Empty;

        /// <summary>
        /// 最后一次提交的消息摘要。
        /// </summary>
        [ObservableProperty]
        private string _lastCommitMessage = string.Empty;

        /// <summary>
        /// 最近更新时间（从文件系统时间推断）。
        /// </summary>
        [ObservableProperty]
        private DateTime? _lastUpdated;

        /// <summary>
        /// 插件所在的完整目录路径（用于执行删除/打开等操作）。
        /// </summary>
        [ObservableProperty]
        private string _path = string.Empty;
    }
}
