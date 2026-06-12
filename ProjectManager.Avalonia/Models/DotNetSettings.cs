namespace ProjectManager.Avalonia.Models;

public class DotNetSettings
{
    public string TargetFramework { get; set; } = "net8.0";
    public string ProjectType { get; set; } = "Web API";
    public int Port { get; set; } = 5000;
    public bool EnableHotReload { get; set; }
    public bool EnableHttpsRedirection { get; set; }
    public bool EnableDeveloperExceptionPage { get; set; }
    public string BuildConfiguration { get; set; } = "Debug"; // Debug|Release
    public string BuildCommand { get; set; } = "dotnet build";
    public string TestCommand { get; set; } = "dotnet test";
    public string OutputPath { get; set; } = "./bin/Debug";
    public bool RunTestsBeforeBuild { get; set; }
    public bool EnableCodeAnalysis { get; set; }
    public bool TreatWarningsAsErrors { get; set; }
    public string PublishCommand { get; set; } = "dotnet publish";
    public string TargetRuntime { get; set; } = "portable";
    public string PublishPath { get; set; } = "./publish";
    public bool SingleFilePublish { get; set; }
    public bool SelfContainedPublish { get; set; }
    public bool EnableReadyToRun { get; set; }
}
