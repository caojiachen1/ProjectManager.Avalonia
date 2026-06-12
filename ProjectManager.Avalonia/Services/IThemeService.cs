using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Manages application theme (Light/Dark/System).
/// </summary>
public interface IThemeService
{
    bool IsDarkMode { get; }
    AppThemeMode CurrentTheme { get; }
    void SetTheme(bool isDark);
    void SetThemeMode(AppThemeMode mode);
    void ToggleTheme();
    System.Threading.Tasks.Task InitializeAsync();
}
