using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// Git信息模型
    /// </summary>
    public partial class GitInfo : ObservableObject
    {
        [ObservableProperty]
        private bool _isGitRepository = false;

        [ObservableProperty]
        private string _currentBranch = string.Empty;

        [ObservableProperty]
        private string _remoteUrl = string.Empty;

        [ObservableProperty]
        private int _uncommittedChanges = 0;

        [ObservableProperty]
        private int _unpushedCommits = 0;

        [ObservableProperty]
        private string _lastCommitMessage = string.Empty;

        [ObservableProperty]
        private DateTime _lastCommitDate = DateTime.MinValue;

        [ObservableProperty]
        private string _lastCommitAuthor = string.Empty;

        [ObservableProperty]
        private List<string> _branches = new();

        [ObservableProperty]
        private GitStatus _status = GitStatus.Clean;

        // StatusDisplay uses hardcoded strings for now (Phase 8 will add i18n)
        public string StatusDisplay => Status switch
        {
            GitStatus.Clean => "Clean",
            GitStatus.Modified => "Modified",
            GitStatus.Staged => "Staged",
            GitStatus.Conflicted => "Conflicted",
            GitStatus.Untracked => "Untracked",
            _ => "Unknown"
        };

        public string LastCommitDateDisplay => LastCommitDate == DateTime.MinValue 
            ? "无提交" 
            : LastCommitDate.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public enum GitStatus
    {
        Clean,      // 干净状态
        Modified,   // 有修改
        Staged,     // 已暂存
        Conflicted, // 有冲突
        Untracked   // 有未跟踪文件
    }
}
