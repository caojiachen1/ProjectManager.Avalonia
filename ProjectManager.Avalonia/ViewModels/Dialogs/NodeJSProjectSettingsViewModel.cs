using Avalonia.Platform.Storage;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class NodeJSProjectSettingsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private Project? _project;
    private bool _isLoadingProject;

    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _projectPath = string.Empty;
    [ObservableProperty] private string _startCommand = "npm start";
    [ObservableProperty] private int _port = 3000;
    [ObservableProperty] private string _nodeVersion = string.Empty;
    [ObservableProperty] private string _packageManager = "npm";
    [ObservableProperty] private bool _developmentMode;
    [ObservableProperty] private bool _hotReload;
    [ObservableProperty] private bool _debugMode;
    [ObservableProperty] private string _buildCommand = "npm run build";
    [ObservableProperty] private string _testCommand = "npm test";
    [ObservableProperty] private string _buildOutputPath = "./dist";
    [ObservableProperty] private bool _runTestsBeforeBuild;
    [ObservableProperty] private bool _minifyOutput;
    [ObservableProperty] private string _environmentFile = ".env";
    [ObservableProperty] private string _customEnvironmentVars = string.Empty;
    [ObservableProperty] private string _tagsString = "JavaScript,Node.js,后端,全栈";

    public event EventHandler<Project>? ProjectSaved;
    public event EventHandler<string>? ProjectDeleted;
    public event EventHandler? DialogCancelled;

    public NodeJSProjectSettingsViewModel(IProjectService projectService)
    {
        _projectService = projectService;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DevelopmentMode) or nameof(HotReload) or nameof(DebugMode) or nameof(Port) or nameof(PackageManager))
        {
            if (!_isLoadingProject) UpdateStartCommand();
        }
    }

    private void UpdateStartCommand()
    {
        var baseCommand = PackageManager switch
        {
            "yarn" => "yarn",
            "pnpm" => "pnpm",
            _ => "npm run"
        };
        var command = HotReload ? $"{baseCommand} dev" : $"{baseCommand} start";
        if (DebugMode) command = command.Replace("start", "start --inspect");
        StartCommand = command;
    }

    public void LoadProject(Project project)
    {
        _project = project;
        _isLoadingProject = true;

        ProjectName = project.Name ?? string.Empty;
        ProjectPath = project.LocalPath ?? string.Empty;
        StartCommand = project.StartCommand ?? string.Empty;
        TagsString = project.Tags != null ? string.Join(", ", project.Tags) : "JavaScript,Node.js,后端,全栈";

        if (!string.IsNullOrWhiteSpace(project.StartCommand))
            ParseStartCommand(project.StartCommand);

        if (project.NodeJSSettings != null)
        {
            var s = project.NodeJSSettings;
            Port = s.Port; NodeVersion = s.NodeVersion; PackageManager = s.PackageManager;
            DevelopmentMode = s.DevelopmentMode; HotReload = s.HotReload; DebugMode = s.DebugMode;
            BuildCommand = s.BuildCommand; TestCommand = s.TestCommand; BuildOutputPath = s.BuildOutputPath;
            RunTestsBeforeBuild = s.RunTestsBeforeBuild; MinifyOutput = s.MinifyOutput;
            EnvironmentFile = s.EnvironmentFile; CustomEnvironmentVars = s.CustomEnvironmentVars;
        }
        _isLoadingProject = false;
    }

    private void ParseStartCommand(string command)
    {
        HotReload = command.Contains("dev");
        DebugMode = command.Contains("--inspect");
        DevelopmentMode = command.Contains("NODE_ENV=development");
        if (command.StartsWith("yarn")) PackageManager = "yarn";
        else if (command.StartsWith("pnpm")) PackageManager = "pnpm";
        else PackageManager = "npm";
    }

    [RelayCommand]
    private async Task BrowseBuildOutputPath()
    {
        var path = await BrowseFolderAsync("选择构建输出文件夹");
        if (path != null) BuildOutputPath = path;
    }

    [RelayCommand]
    private async Task BrowseEnvironmentFile()
    {
        var file = await BrowseFileAsync("选择环境变量文件", ProjectPath,
            new[] { new FilePickerFileType("环境变量文件") { Patterns = new[] { "*.env" } }, FilePickerFileTypes.All });
        if (file != null) EnvironmentFile = file;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_project == null) return;
        try
        {
            _project.StartCommand = StartCommand;
            _project.LastModified = DateTime.Now;

            if (_project.Framework.Equals("Node.js", StringComparison.OrdinalIgnoreCase))
            {
                _project.NodeJSSettings = new NodeJSSettings
                {
                    Port = Port, NodeVersion = NodeVersion, PackageManager = PackageManager,
                    DevelopmentMode = DevelopmentMode, HotReload = HotReload, DebugMode = DebugMode,
                    BuildCommand = BuildCommand, TestCommand = TestCommand, BuildOutputPath = BuildOutputPath,
                    RunTestsBeforeBuild = RunTestsBeforeBuild, MinifyOutput = MinifyOutput,
                    EnvironmentFile = EnvironmentFile, CustomEnvironmentVars = CustomEnvironmentVars
                };
            }

            if (!string.IsNullOrWhiteSpace(TagsString))
                _project.Tags = TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            else
                _project.Tags = new List<string>();

            if (await _projectService.SaveProjectAsync(_project))
                ProjectSaved?.Invoke(this, _project);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存项目失败: {ex.Message}"); }
    }

    [RelayCommand]
    private void Cancel() => DialogCancelled?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task Delete()
    {
        if (_project != null)
        {
            try { await _projectService.DeleteProjectAsync(_project.Id); ProjectDeleted?.Invoke(this, _project.Id); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"删除项目失败: {ex.Message}"); }
        }
    }
}
