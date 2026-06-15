using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly ILanguageService _languageService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _appTitle = "通用项目管理器";

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private FANavigationViewItem? _selectedItem;

    [ObservableProperty]
    private string _navSearchText = string.Empty;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    public ObservableCollection<FANavigationViewItem> MenuItems { get; } = new();
    public ObservableCollection<FANavigationViewItem> FooterMenuItems { get; } = new();

    private List<FANavigationViewItem> _allMenuItems = new();
    private List<FANavigationViewItem> _allFooterMenuItems = new();

    // Segoe Fluent Icons 字体，与 WPF 项目图标风格一致
    private static readonly FontFamily SegoeFluentIcons = new("Segoe Fluent Icons");

    // 创建与 WPF SymbolRegular 图标对应的 FontIconSource
    // WPF: Home24 → Segoe Fluent Icons Home (U+E80F)
    // WPF: Apps24 → Segoe Fluent Icons AllApps (U+E71D)
    // WPF: WindowConsole20 → Segoe Fluent Icons Console (U+E756)
    // WPF: Pulse24 → Segoe Fluent Icons Pulse (U+E977)
    // WPF: BracesVariable24 → Segoe Fluent Icons Code (U+E943)
    // WPF: Settings24 → Segoe Fluent Icons Settings (U+E713)
    private static FAFontIconSource CreateNavIcon(string glyph) =>
        new() { Glyph = glyph, FontFamily = SegoeFluentIcons, FontSize = 16 };

    public MainWindowViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ILanguageService languageService,
        ISettingsService settingsService)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _languageService = languageService;
        _settingsService = settingsService;

        BuildNavigationItems();

        _languageService.LanguageChanged += OnLanguageChanged;
    }

    partial void OnNavSearchTextChanged(string value)
    {
        FilterNavigationItems();
    }

    private void BuildNavigationItems()
    {
        MenuItems.Clear();
        FooterMenuItems.Clear();
        _allMenuItems.Clear();
        _allFooterMenuItems.Clear();

        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Dashboard"),
            IconSource = CreateNavIcon("\uE80F"), // Home (WPF: Home24)
            Tag = typeof(DashboardViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Projects"),
            IconSource = CreateNavIcon("\uE71D"), // AllApps (WPF: Apps24)
            Tag = typeof(ProjectsViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Terminal"),
            IconSource = CreateNavIcon("\uE756"), // Console (WPF: WindowConsole20)
            Tag = typeof(TerminalViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Performance"),
            IconSource = CreateNavIcon("\uE977"), // Pulse (WPF: Pulse24)
            Tag = typeof(PerformanceViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Environment"),
            IconSource = CreateNavIcon("\uE943"), // Code/Braces (WPF: BracesVariable24)
            Tag = typeof(SystemEnvironmentVariablesViewModel)
        });

        _allFooterMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Settings"),
            IconSource = CreateNavIcon("\uE713"), // Settings (WPF: Settings24)
            Tag = typeof(SettingsViewModel)
        });

        FilterNavigationItems();
    }

    private void FilterNavigationItems()
    {
        var searchText = NavSearchText?.Trim() ?? string.Empty;

        MenuItems.Clear();
        foreach (var item in _allMenuItems)
        {
            if (string.IsNullOrEmpty(searchText) ||
                (item.Content?.ToString()?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true))
            {
                MenuItems.Add(item);
            }
        }

        FooterMenuItems.Clear();
        foreach (var item in _allFooterMenuItems)
        {
            if (string.IsNullOrEmpty(searchText) ||
                (item.Content?.ToString()?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true))
            {
                FooterMenuItems.Add(item);
            }
        }

        if (!string.IsNullOrEmpty(searchText) && MenuItems.Count == 0 && FooterMenuItems.Count == 0)
        {
            SelectedItem = null;
        }
        else if (SelectedItem != null && !MenuItems.Contains(SelectedItem) && !FooterMenuItems.Contains(SelectedItem))
        {
            SelectedItem = MenuItems.FirstOrDefault() ?? FooterMenuItems.FirstOrDefault();
        }
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        AppTitle = _languageService.GetString("AppTitle");
        UpdateNavigationLabels();
    }

    private void UpdateNavigationLabels()
    {
        var keys = new[] { "Nav_Dashboard", "Nav_Projects", "Nav_Terminal", "Nav_Performance", "Nav_Environment" };
        for (int i = 0; i < _allMenuItems.Count && i < keys.Length; i++)
        {
            _allMenuItems[i].Content = _languageService.GetString(keys[i]);
        }
        if (_allFooterMenuItems.Count > 0)
        {
            _allFooterMenuItems[0].Content = _languageService.GetString("Nav_Settings");
        }
        FilterNavigationItems();
    }

    public async Task NavigateToDefaultAsync()
    {
        if (MenuItems.Count == 0) return;

        string defaultPage = "Dashboard";
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            defaultPage = settings.DefaultStartupPage;
        }
        catch
        {
            // 读取设置失败时回退到 Dashboard
        }

        var pageType = defaultPage switch
        {
            "Dashboard" => typeof(DashboardViewModel),
            "Projects"  => typeof(ProjectsViewModel),
            "Terminal"  => typeof(TerminalViewModel),
            "Performance" => typeof(PerformanceViewModel),
            _ => typeof(DashboardViewModel)
        };

        var item = MenuItems.FirstOrDefault(i => i.Tag is Type t && t == pageType)
                ?? MenuItems[0];
        NavigateToItem(item);
    }

    /// <summary>
    /// 编程式导航到指定的 ViewModel 页面，显式触发生命周期方法
    /// </summary>
    public void NavigateToViewModel(Type viewModelType)
    {
        var allItems = MenuItems.Concat(FooterMenuItems);
        var item = allItems.FirstOrDefault(i => i.Tag is Type t && t == viewModelType);
        if (item != null)
        {
            NavigateToItem(item);
        }
    }

    /// <summary>
    /// 核心导航方法：切换页面并正确触发所有生命周期方法
    /// </summary>
    private void NavigateToItem(FANavigationViewItem item)
    {
        if (item.Tag is not Type pageType) return;

        var vm = App.Services.GetService(pageType) as ViewModelBase;
        if (vm == null) return;

        // Call OnNavigatedFrom on the previous page
        var oldPage = CurrentPage;
        if (oldPage != null)
        {
            var onFrom = oldPage.GetType().GetMethod("OnNavigatedFrom");
            onFrom?.Invoke(oldPage, null);
        }

        // Update selection and current page
        SelectedItem = item;
        CurrentPage = vm;

        // Call OnNavigatedTo/OnNavigatedToAsync on the new page
        var onToAsync = vm.GetType().GetMethod("OnNavigatedToAsync");
        if (onToAsync != null)
        {
            onToAsync.Invoke(vm, null);
        }
        else
        {
            vm.GetType().GetMethod("OnNavigatedTo")?.Invoke(vm, null);
        }
    }

    public void HandleNavigationSelectionChanged(FANavigationViewSelectionChangedEventArgs? args)
    {
        if (args?.SelectedItem is FANavigationViewItem item && item.Tag is Type pageType)
        {
            var vm = App.Services.GetService(pageType) as ViewModelBase;
            if (vm == null) return;

            // Avoid double-triggering lifecycle if already on this page (from programmatic navigation)
            if (ReferenceEquals(CurrentPage, vm)) return;

            // Call OnNavigatedFrom on the previous page
            var oldPage = CurrentPage;
            if (oldPage != null)
            {
                var onFrom = oldPage.GetType().GetMethod("OnNavigatedFrom");
                onFrom?.Invoke(oldPage, null);
            }

            CurrentPage = vm;

            // Call OnNavigatedTo on the new page
            var onToAsync = vm.GetType().GetMethod("OnNavigatedToAsync");
            if (onToAsync != null)
            {
                onToAsync.Invoke(vm, null);
            }
            else
            {
                vm.GetType().GetMethod("OnNavigatedTo")?.Invoke(vm, null);
            }
        }
    }
}
