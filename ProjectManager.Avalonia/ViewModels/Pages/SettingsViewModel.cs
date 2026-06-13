using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Pages;

/// <summary>
/// 启动页面选项，Name 为显示文本，Value 为持久化值（如 "Dashboard"）
/// </summary>
public record StartupPageOption(string Name, string Value);

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly ILanguageService _languageService;

    /// <summary>
    /// True once InitializeViewModelAsync has finished assigning initial property values.
    /// Used by the view's code-behind to suppress SelectionChanged side effects during load.
    /// </summary>
    public bool IsInitialized { get; private set; }

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private AppThemeMode _theme = AppThemeMode.System;

    [ObservableProperty]
    private string _gitUserName = string.Empty;

    [ObservableProperty]
    private string _gitUserEmail = string.Empty;

    [ObservableProperty]
    private string _gitExecutablePath = string.Empty;

    [ObservableProperty]
    private string _defaultProjectPath = string.Empty;

    [ObservableProperty]
    private bool _autoStartProjects;

    [ObservableProperty]
    private string _defaultGitBranch = "main";

    [ObservableProperty]
    private bool _autoFetchGitUpdates = true;

    [ObservableProperty]
    private int _projectRefreshInterval = 30;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private string _preferredTerminal = "PowerShell";

    [ObservableProperty]
    private string _preferredEditor = "VS Code";

    [ObservableProperty]
    private bool _autoSaveProjects = true;

    [ObservableProperty]
    private int _maxRecentProjects = 10;

    [ObservableProperty]
    private bool _useCmdChcp65001 = true;

    [ObservableProperty]
    private string _defaultStartupPage = "Dashboard";

    [ObservableProperty]
    private bool _showTerminalTimestamps;

    [ObservableProperty]
    private string _selectedLanguage = "zh-CN";

    [ObservableProperty]
    private LanguageInfo? _selectedLanguageInfo;

    [ObservableProperty]
    private ObservableCollection<LanguageInfo> _availableLanguages = new();

    [ObservableProperty]
    private ObservableCollection<string> _terminalOptions = new() { "PowerShell", "CMD", "Git Bash" };

    [ObservableProperty]
    private ObservableCollection<StartupPageOption> _startupPageOptions = new();

    [ObservableProperty]
    private StartupPageOption? _selectedStartupPageOption;

    public SettingsViewModel(
        IThemeService themeService,
        ISettingsService settingsService,
        ILanguageService languageService)
    {
        _themeService = themeService;
        _settingsService = settingsService;
        _languageService = languageService;

        foreach (var lang in _languageService.SupportedLanguages)
            AvailableLanguages.Add(lang);

        PropertyChanged += OnPropertyChanged;
    }

    private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!IsInitialized) return;

        if (e.PropertyName == nameof(SelectedLanguage))
        {
            SelectedLanguageInfo = AvailableLanguages.FirstOrDefault(l => l.Code == SelectedLanguage);
            if (!string.IsNullOrEmpty(SelectedLanguage))
                _languageService.ChangeLanguage(SelectedLanguage);
            // 语言变化时重新生成启动页面选项的显示名
            BuildStartupPageOptions();
            return;
        }

        if (e.PropertyName == nameof(Theme))
        {
            _themeService.SetThemeMode(Theme);
        }

        // SelectedStartupPageOption 变化时同步到 DefaultStartupPage
        if (e.PropertyName == nameof(SelectedStartupPageOption) && SelectedStartupPageOption != null)
        {
            if (DefaultStartupPage != SelectedStartupPageOption.Value)
                DefaultStartupPage = SelectedStartupPageOption.Value;
        }

        // DefaultStartupPage 变化时同步 SelectedStartupPageOption
        if (e.PropertyName == nameof(DefaultStartupPage))
        {
            var match = StartupPageOptions.FirstOrDefault(o => o.Value == DefaultStartupPage);
            if (match != null && !ReferenceEquals(SelectedStartupPageOption, match))
                SelectedStartupPageOption = match;
        }

        if (e.PropertyName != nameof(AppVersion) &&
            e.PropertyName != nameof(AvailableLanguages) &&
            e.PropertyName != nameof(SelectedLanguageInfo) &&
            e.PropertyName != nameof(TerminalOptions) &&
            e.PropertyName != nameof(StartupPageOptions) &&
            e.PropertyName != nameof(SelectedStartupPageOption))
        {
            await SaveAllSettingsAsync();
        }
    }

    public async Task OnNavigatedToAsync()
    {
        if (!IsInitialized)
            await InitializeViewModelAsync();
    }

    public async Task OnNavigatedFromAsync()
    {
        await SaveAllSettingsAsync();
    }

    private async Task InitializeViewModelAsync()
    {
        AppVersion = $"项目管理器 - {GetAssemblyVersion()}";

        var settings = await _settingsService.GetSettingsAsync();

        Theme = settings.Theme == AppThemeMode.Unknown ? AppThemeMode.System : settings.Theme;
        _themeService.SetThemeMode(Theme);
        GitUserName = settings.GitUserName;
        GitUserEmail = settings.GitUserEmail;
        GitExecutablePath = settings.GitExecutablePath;
        DefaultProjectPath = settings.DefaultProjectPath;
        AutoStartProjects = settings.AutoStartProjects;
        DefaultGitBranch = settings.DefaultGitBranch;
        AutoFetchGitUpdates = settings.AutoFetchGitUpdates;
        ProjectRefreshInterval = settings.ProjectRefreshInterval;
        ShowNotifications = settings.ShowNotifications;
        PreferredTerminal = settings.PreferredTerminal;
        AutoSaveProjects = settings.AutoSaveProjects;
        MaxRecentProjects = settings.MaxRecentProjects;
        UseCmdChcp65001 = settings.UseCmdChcp65001;
        DefaultStartupPage = settings.DefaultStartupPage;
        ShowTerminalTimestamps = settings.ShowTerminalTimestamps;
        SelectedLanguage = _languageService.CurrentLanguage;
        SelectedLanguageInfo = AvailableLanguages.FirstOrDefault(l => l.Code == SelectedLanguage);

        BuildStartupPageOptions();

        IsInitialized = true;
    }

    private void BuildStartupPageOptions()
    {
        var currentValue = DefaultStartupPage;
        StartupPageOptions = new ObservableCollection<StartupPageOption>
        {
            new(_languageService.GetString("Nav_Dashboard"), "Dashboard"),
            new(_languageService.GetString("Nav_Projects"),  "Projects"),
            new(_languageService.GetString("Nav_Terminal"),  "Terminal"),
            new(_languageService.GetString("Nav_Performance"), "Performance"),
        };
        SelectedStartupPageOption = StartupPageOptions.FirstOrDefault(o => o.Value == currentValue)
                                  ?? StartupPageOptions[0];
    }

    private async Task SaveAllSettingsAsync()
    {
        var settings = new AppSettings
        {
            GitUserName = GitUserName,
            GitUserEmail = GitUserEmail,
            GitExecutablePath = GitExecutablePath,
            DefaultProjectPath = DefaultProjectPath,
            AutoStartProjects = AutoStartProjects,
            DefaultGitBranch = DefaultGitBranch,
            AutoFetchGitUpdates = AutoFetchGitUpdates,
            ProjectRefreshInterval = ProjectRefreshInterval,
            ShowNotifications = ShowNotifications,
            PreferredTerminal = PreferredTerminal,
            AutoSaveProjects = AutoSaveProjects,
            MaxRecentProjects = MaxRecentProjects,
            UseCmdChcp65001 = UseCmdChcp65001,
            DefaultStartupPage = DefaultStartupPage,
            ShowTerminalTimestamps = ShowTerminalTimestamps,
            Theme = Theme,
            Language = SelectedLanguage
        };

        await _settingsService.SaveSettingsAsync(settings);
    }

    private string GetAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }

    [RelayCommand]
    private async Task BrowseDefaultProjectPath()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择默认项目路径",
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(DefaultProjectPath)
        });

        if (result.Count > 0)
        {
            DefaultProjectPath = result[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseGitExecutablePath()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择Git可执行文件",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("可执行文件") { Patterns = new[] { "*.exe" } },
                FilePickerFileTypes.All
            },
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            GitExecutablePath = result[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        await SaveAllSettingsAsync();
    }

    [RelayCommand]
    private async Task ResetSettings()
    {
        var settings = new AppSettings
        {
            DefaultProjectPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Projects"),
            DefaultStartupPage = "Dashboard",
            Language = "zh-CN"
        };

        await _settingsService.SaveSettingsAsync(settings);
        await InitializeViewModelAsync();
    }

    [RelayCommand]
    private void ChangeLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode)) return;
        _languageService.ChangeLanguage(languageCode);
        SelectedLanguage = languageCode;
    }
}
