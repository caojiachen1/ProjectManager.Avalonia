namespace ProjectManager.Avalonia.Models;

public class NodeJSSettings
{
    public int Port { get; set; } = 3000;
    public string NodeVersion { get; set; } = string.Empty;
    public string PackageManager { get; set; } = "npm"; // npm|yarn|pnpm
    public bool DevelopmentMode { get; set; }
    public bool HotReload { get; set; }
    public bool DebugMode { get; set; }
    public string BuildCommand { get; set; } = "npm run build";
    public string TestCommand { get; set; } = "npm test";
    public string BuildOutputPath { get; set; } = "./dist";
    public bool RunTestsBeforeBuild { get; set; }
    public bool MinifyOutput { get; set; }
    public string EnvironmentFile { get; set; } = ".env";
    public string CustomEnvironmentVars { get; set; } = string.Empty;
}
