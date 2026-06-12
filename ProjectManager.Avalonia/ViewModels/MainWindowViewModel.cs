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

    [ObservableProperty]
    private string _appTitle = "通用项目管理器";

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private FANavigationViewItem? _selectedItem;

    public ObservableCollection<FANavigationViewItem> MenuItems { get; } = new();
    public ObservableCollection<FANavigationViewItem> FooterMenuItems { get; } = new();

    public MainWindowViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ILanguageService languageService)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _languageService = languageService;

        BuildNavigationItems();

        _languageService.LanguageChanged += OnLanguageChanged;
    }

    private void BuildNavigationItems()
    {
        MenuItems.Clear();
        FooterMenuItems.Clear();

        MenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Dashboard"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Home },
            Tag = typeof(DashboardViewModel)
        });
        MenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Projects"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Folder },
            Tag = typeof(ProjectsViewModel)
        });
        MenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Terminal"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Code },
            Tag = typeof(TerminalViewModel)
        });
        MenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Performance"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.List },
            Tag = typeof(PerformanceViewModel)
        });
        MenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Environment"),
            IconSource = new FASymbolIconSource { Symbol = (FASymbol)0xE8A5 },
            Tag = typeof(SystemEnvironmentVariablesViewModel)
        });

        FooterMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Settings"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Settings },
            Tag = typeof(SettingsViewModel)
        });
    }

    private void OnLanguageChanged(object? sender, string languageCode)
    {
        AppTitle = _languageService.GetString("AppTitle");
        UpdateNavigationLabels();
    }

    private void UpdateNavigationLabels()
    {
        var keys = new[] { "Nav_Dashboard", "Nav_Projects", "Nav_Terminal", "Nav_Performance", "Nav_Environment" };
        for (int i = 0; i < MenuItems.Count && i < keys.Length; i++)
        {
            MenuItems[i].Content = _languageService.GetString(keys[i]);
        }
        if (FooterMenuItems.Count > 0)
        {
            FooterMenuItems[0].Content = _languageService.GetString("Nav_Settings");
        }
    }

    public void NavigateToDefault()
    {
        if (MenuItems.Count > 0)
        {
            NavigateToItem(MenuItems[0]);
        }
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
