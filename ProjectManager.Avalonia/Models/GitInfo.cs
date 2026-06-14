using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    public partial class GitInfo : ObservableObject
    {
        private static string R(string key, string fallback)
        {
            var app = Application.Current;
            return (app?.Resources.TryGetResource(key, null, out var res) == true && res is string s) ? s : fallback;
        }

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

        public string StatusDisplay => Status switch
        {
            GitStatus.Clean => R("GitStatus_Clean", "Clean"),
            GitStatus.Modified => R("GitStatus_Modified", "Modified"),
            GitStatus.Staged => R("GitStatus_Staged", "Staged"),
            GitStatus.Conflicted => R("GitStatus_Conflicted", "Conflicted"),
            GitStatus.Untracked => R("GitStatus_Untracked", "Untracked"),
            _ => R("Status_Unknown", "Unknown")
        };

        public string LastCommitDateDisplay => LastCommitDate == DateTime.MinValue 
            ? R("Git_NoCommit", "No commits") 
            : LastCommitDate.ToString("yyyy-MM-dd HH:mm:ss");

        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(LastCommitDateDisplay));
        }
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
