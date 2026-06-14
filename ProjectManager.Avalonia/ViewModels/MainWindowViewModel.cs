using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    public ObservableCollection<FANavigationViewItem> MenuItems { get; } = new();
    public ObservableCollection<FANavigationViewItem> FooterMenuItems { get; } = new();

    private List<FANavigationViewItem> _allMenuItems = new();
    private List<FANavigationViewItem> _allFooterMenuItems = new();

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
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Home },
            Tag = typeof(DashboardViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Projects"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Folder },
            Tag = typeof(ProjectsViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Terminal"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Code },
            Tag = typeof(TerminalViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Performance"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.List },
            Tag = typeof(PerformanceViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Environment"),
            IconSource = new FASymbolIconSource { Symbol = (FASymbol)0xE8A5 },
            Tag = typeof(SystemEnvironmentVariablesViewModel)
        });

        _allFooterMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Settings"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Settings },
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
