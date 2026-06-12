using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    public partial class Project : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _localPath = string.Empty;

        [ObservableProperty]
        private string _startCommand = string.Empty;

        [ObservableProperty]
        private string _workingDirectory = string.Empty;

        [ObservableProperty]
        private string _framework = string.Empty;

        [ObservableProperty]
        [JsonIgnore]
        private GitInfo? _gitInfo = new();

        [ObservableProperty]
        private DateTime _createdDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _lastModified = DateTime.Now;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusDisplay))]
        private ProjectStatus _status = ProjectStatus.Stopped;

        [ObservableProperty]
        private int? _processId;

        [ObservableProperty]
        [JsonIgnore]
        private Process? _runningProcess;

        [ObservableProperty]
        private string _logOutput = string.Empty;

        [ObservableProperty]
        private List<string> _tags = new();

        [ObservableProperty]
        private bool _autoStart = false;

        [ObservableProperty]
        private Dictionary<string, string> _environmentVariables = new();

        [ObservableProperty]
        private List<string> _gitRepositories = new();

        // ComfyUI 专用设置（可为空以保持向后兼容）
        [ObservableProperty]
        private ComfyUISettings? _comfyUISettings;

        // Node.js 设置（可为空）
        [ObservableProperty]
        private NodeJSSettings? _nodeJSSettings;

        // .NET 设置（可为空）
        [ObservableProperty]
        private DotNetSettings? _dotNetSettings;

        // StatusDisplay uses hardcoded strings for now (Phase 8 will add i18n)
        [JsonIgnore]
        public string StatusDisplay => Status switch
        {
            ProjectStatus.Running => "Running",
            ProjectStatus.Stopped => "Stopped",
            ProjectStatus.Starting => "Starting",
            ProjectStatus.Stopping => "Stopping",
            ProjectStatus.Error => "Error",
            _ => "Unknown"
        };

        [JsonIgnore]
        public string LastModifiedDisplay => LastModified.ToString("yyyy-MM-dd HH:mm:ss");

        [JsonIgnore]
        public string CreatedDateDisplay => CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public enum ProjectStatus
    {
        [Description("已停止")]
        Stopped,
        [Description("运行中")]
        Running,
        [Description("启动中")]
        Starting,
        [Description("停止中")]
        Stopping,
        [Description("错误")]
        Error
    }
}
