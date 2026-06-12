using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Manages application theme (Light/Dark/System) using Avalonia's RequestedThemeVariant.
/// Apply-only: persistence is handled by SettingsViewModel.SaveAllSettingsAsync.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private AppThemeMode _currentTheme = AppThemeMode.System;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsDarkMode
    {
        get
        {
            if (Application.Current is { } app)
                return app.ActualThemeVariant == ThemeVariant.Dark;
            return false;
        }
    }

    public AppThemeMode CurrentTheme => _currentTheme;

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            SetThemeMode(settings.Theme);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load theme setting: {ex.Message}");
            SetThemeMode(AppThemeMode.System);
        }
    }

    public void SetTheme(bool isDark)
    {
        SetThemeMode(isDark ? AppThemeMode.Dark : AppThemeMode.Light);
    }

    public void SetThemeMode(AppThemeMode mode)
    {
        _currentTheme = mode;

        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode switch
            {
                AppThemeMode.Light => ThemeVariant.Light,
                AppThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    public void ToggleTheme() => SetTheme(!IsDarkMode);
}
