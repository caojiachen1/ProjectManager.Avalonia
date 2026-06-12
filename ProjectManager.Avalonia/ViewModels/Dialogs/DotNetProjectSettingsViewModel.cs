using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Dialogs;

public partial class DotNetProjectSettingsViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private Project? _project;
    private bool _isLoadingProject;
    private bool _isUpdatingFromCommand;

    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _projectPath = string.Empty;
    [ObservableProperty] private string _startCommand = "dotnet run";
    [ObservableProperty] private string _targetFramework = "net8.0";
    [ObservableProperty] private string _projectType = "Web API";
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private bool _enableHotReload;
    [ObservableProperty] private bool _enableHttpsRedirection;
    [ObservableProperty] private bool _enableDeveloperExceptionPage;
    [ObservableProperty] private string _buildConfiguration = "Debug";
    [ObservableProperty] private string _buildCommand = "dotnet build";
    [ObservableProperty] private string _testCommand = "dotnet test";
    [ObservableProperty] private string _outputPath = "./bin/Debug";
    [ObservableProperty] private bool _runTestsBeforeBuild;
    [ObservableProperty] private bool _enableCodeAnalysis;
    [ObservableProperty] private bool _treatWarningsAsErrors;
    [ObservableProperty] private string _publishCommand = "dotnet publish";
    [ObservableProperty] private string _targetRuntime = "portable";
    [ObservableProperty] private string _publishPath = "./publish";
    [ObservableProperty] private bool _singleFilePublish;
    [ObservableProperty] private bool _selfContainedPublish;
    [ObservableProperty] private bool _enableReadyToRun;
    [ObservableProperty] private string _tagsString = ".NET,C#,后端,Web API";

    public List<string> StartCommandOptions { get; } = new() { "dotnet run", "dotnet watch run", "dotnet run --launch-profile Development", "dotnet run --no-build", "dotnet run --configuration Release", "dotnet run --project ." };
    public List<string> TargetFrameworkOptions { get; } = new() { "net8.0", "net7.0", "net6.0", "netcoreapp3.1", "net48", "netstandard2.1" };
    public List<string> ProjectTypeOptions { get; } = new() { "Web API", "Web App (MVC)", "Blazor Server", "Blazor WebAssembly", "Console App", "Class Library", "Worker Service", "WPF", "WinForms" };
    public List<string> BuildConfigurationOptions { get; } = new() { "Debug", "Release" };
    public List<string> TargetRuntimeOptions { get; } = new() { "portable", "win-x64", "win-x86", "linux-x64", "osx-x64", "osx-arm64" };

    public event EventHandler<Project>? ProjectSaved;
    public event EventHandler<string>? ProjectDeleted;
    public event EventHandler? DialogCancelled;

    public DotNetProjectSettingsViewModel(IProjectService projectService)
    {
        _projectService = projectService;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EnableHotReload) or nameof(BuildConfiguration) or nameof(Port))
        {
            if (!_isLoadingProject) UpdateStartCommand();
        }
        else if (e.PropertyName == nameof(StartCommand))
        {
            if (!_isLoadingProject) ParseStartCommandAndUpdateSettings(StartCommand);
        }
    }

    private void UpdateStartCommand()
    {
        if (_isUpdatingFromCommand) return;
        var command = EnableHotReload ? "dotnet watch run" : "dotnet run";
        if (BuildConfiguration == "Release") command += " --configuration Release";
        if (Port != 5000 && ProjectType.Contains("Web")) command += $" --urls http://localhost:{Port}";
        StartCommand = command;
    }

    public void LoadProject(Project project)
    {
        _project = project;
        _isLoadingProject = true;

        ProjectName = project.Name ?? string.Empty;
        ProjectPath = project.LocalPath ?? string.Empty;
        StartCommand = project.StartCommand ?? string.Empty;
        TagsString = project.Tags != null ? string.Join(", ", project.Tags) : ".NET,C#,后端,Web API";

        if (!string.IsNullOrWhiteSpace(project.StartCommand)) ParseStartCommand(project.StartCommand);

        if (project.DotNetSettings != null)
        {
            var s = project.DotNetSettings;
            TargetFramework = s.TargetFramework; ProjectType = s.ProjectType; Port = s.Port;
            EnableHotReload = s.EnableHotReload; EnableHttpsRedirection = s.EnableHttpsRedirection;
            EnableDeveloperExceptionPage = s.EnableDeveloperExceptionPage;
            BuildConfiguration = s.BuildConfiguration; BuildCommand = s.BuildCommand; TestCommand = s.TestCommand;
            OutputPath = s.OutputPath; RunTestsBeforeBuild = s.RunTestsBeforeBuild;
            EnableCodeAnalysis = s.EnableCodeAnalysis; TreatWarningsAsErrors = s.TreatWarningsAsErrors;
            PublishCommand = s.PublishCommand; TargetRuntime = s.TargetRuntime; PublishPath = s.PublishPath;
            SingleFilePublish = s.SingleFilePublish; SelfContainedPublish = s.SelfContainedPublish;
            EnableReadyToRun = s.EnableReadyToRun;
        }
        _isLoadingProject = false;
    }

    private void ParseStartCommand(string command)
    {
        EnableHotReload = command.Contains("watch");
        BuildConfiguration = command.Contains("--configuration Release") ? "Release" : "Debug";
        var urlsMatch = System.Text.RegularExpressions.Regex.Match(command, @"--urls\s+http://localhost:(\d+)");
        if (urlsMatch.Success && int.TryParse(urlsMatch.Groups[1].Value, out int port)) Port = port;
    }

    private void ParseStartCommandAndUpdateSettings(string command)
    {
        if (_isUpdatingFromCommand || string.IsNullOrWhiteSpace(command)) return;
        _isUpdatingFromCommand = true;
        try
        {
            var newHotReload = command.Contains("watch");
            if (EnableHotReload != newHotReload) EnableHotReload = newHotReload;

            var newBuildConfig = command.Contains("--configuration Release") ? "Release" : "Debug";
            if (BuildConfiguration != newBuildConfig) BuildConfiguration = newBuildConfig;

            var urlsMatch = System.Text.RegularExpressions.Regex.Match(command, @"--urls\s+https?://[^:]*:(\d+)");
            if (urlsMatch.Success && int.TryParse(urlsMatch.Groups[1].Value, out int port) && Port != port)
                Port = port;

            if (command.Contains("--urls") && !ProjectType.Contains("Web") && !ProjectType.Contains("Blazor"))
                ProjectType = "Web API";
            if (command.Contains("https://")) EnableHttpsRedirection = true;

            if (command.Contains("--launch-profile"))
            {
                var profileMatch = System.Text.RegularExpressions.Regex.Match(command, @"--launch-profile\s+(\w+)");
                if (profileMatch.Success && profileMatch.Groups[1].Value.Equals("Development", StringComparison.OrdinalIgnoreCase))
                    EnableDeveloperExceptionPage = true;
            }

            if (command.Contains("watch") && BuildConfiguration == "Debug")
                EnableDeveloperExceptionPage = true;
        }
        finally { _isUpdatingFromCommand = false; }
    }

    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        var path = await BrowseFolderAsync("选择输出文件夹");
        if (path != null) OutputPath = path;
    }

    [RelayCommand]
    private async Task BrowsePublishPath()
    {
        var path = await BrowseFolderAsync("选择发布文件夹");
        if (path != null) PublishPath = path;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_project == null) return;
        try
        {
            _project.StartCommand = StartCommand;
            _project.LastModified = DateTime.Now;

            if (_project.Framework.Equals(".NET", StringComparison.OrdinalIgnoreCase))
            {
                _project.DotNetSettings = new DotNetSettings
                {
                    TargetFramework = TargetFramework, ProjectType = ProjectType, Port = Port,
                    EnableHotReload = EnableHotReload, EnableHttpsRedirection = EnableHttpsRedirection,
                    EnableDeveloperExceptionPage = EnableDeveloperExceptionPage,
                    BuildConfiguration = BuildConfiguration, BuildCommand = BuildCommand, TestCommand = TestCommand,
                    OutputPath = OutputPath, RunTestsBeforeBuild = RunTestsBeforeBuild,
                    EnableCodeAnalysis = EnableCodeAnalysis, TreatWarningsAsErrors = TreatWarningsAsErrors,
                    PublishCommand = PublishCommand, TargetRuntime = TargetRuntime, PublishPath = PublishPath,
                    SingleFilePublish = SingleFilePublish, SelfContainedPublish = SelfContainedPublish,
                    EnableReadyToRun = EnableReadyToRun
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
            try
            {
                await _projectService.DeleteProjectAsync(_project.Id);
                ProjectDeleted?.Invoke(this, _project.Id);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"删除项目失败: {ex.Message}"); }
        }
    }
}
