namespace ProjectManager.Avalonia.Models;

/// <summary>
/// 主题模式。枚举值 0/1/2 与 WPF 项目的 Wpf.Ui.Appearance.ApplicationTheme 保持一致，
/// 以确保两个项目共享同一个 settings.json 时不会冲突。
/// </summary>
public enum AppThemeMode { Unknown = 0, Light = 1, Dark = 2, System = 3 }

public class AppSettings
{
    // Git设置
    public string GitExecutablePath { get; set; } = string.Empty;
    public string GitUserName { get; set; } = string.Empty;
    public string GitUserEmail { get; set; } = string.Empty;
    public string DefaultGitBranch { get; set; } = "main";
    public bool AutoFetchGitUpdates { get; set; } = true;

    // 项目设置
    public string DefaultProjectPath { get; set; } = string.Empty;
    public bool AutoStartProjects { get; set; } = false;
    public bool AutoSaveProjects { get; set; } = true;
    public int ProjectRefreshInterval { get; set; } = 30;
    public int MaxRecentProjects { get; set; } = 10;

    // 应用程序设置
    // 根据平台自动选择默认终端：Windows 用 PowerShell，Linux/macOS 用 Bash
    public string PreferredTerminal { get; set; } = GetDefaultTerminal();
    public bool ShowNotifications { get; set; } = true;
    public bool UseCmdChcp65001 { get; set; } = true;
    public bool ShowTerminalTimestamps { get; set; } = false;

    // 个性化设置
    public AppThemeMode Theme { get; set; } = AppThemeMode.Unknown;
    public string DefaultStartupPage { get; set; } = "Dashboard";

    // 语言设置
    public string Language { get; set; } = "zh-CN";

    /// <summary>
    /// 根据当前操作系统返回默认终端名称
    /// </summary>
    private static string GetDefaultTerminal()
    {
        if (OperatingSystem.IsWindows())
            return "PowerShell";
        if (OperatingSystem.IsMacOS())
            return "Zsh";
        return "Bash";
    }
}
