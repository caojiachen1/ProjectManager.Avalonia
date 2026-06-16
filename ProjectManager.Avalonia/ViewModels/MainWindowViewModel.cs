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

    /// <summary>
    /// Reentrancy guard: prevents HandleNavigationSelectionChanged from double-triggering
    /// lifecycle methods when NavigateToItem sets SelectedItem programmatically.
    /// </summary>
    private bool _isNavigatingProgrammatically;

    // 使用 FAPathIconSource + SVG 几何数据渲染导航图标
    // 与原 WPF 项目完全一致的图标映射（路径数据来自 Fluent UI System Icons）：
    // Home24, Apps24, WindowConsole20, Pulse24, BracesVariable24, Settings24

    private static FAIconSource CreateNavIcon(string geometry) =>
        new FAPathIconSource
        {
            Data = Geometry.Parse(geometry)
        };

    // SVG path data from https://github.com/microsoft/fluentui-system-icons
    private const string HomeGeometry =
        "M10.5495 2.53189C11.3874 1.82531 12.6126 1.82531 13.4505 2.5319L20.2005 8.224C20.7074 8.65152 21 9.2809 21 9.94406L21 19.2539C21 20.2204 20.2165 21.0039 19.25 21.0039H15.75C14.7835 21.0039 14 20.2204 14 19.2539L14 14.2468C14 14.1088 13.8881 13.9968 13.75 13.9968H10.25C10.1119 13.9968 9.99999 14.1088 9.99999 14.2468L9.99999 19.2539C9.99999 20.2204 9.2165 21.0039 8.25 21.0039H4.75C3.7835 21.0039 3 20.2204 3 19.2539V9.94406C3 9.2809 3.29255 8.65152 3.79952 8.224L10.5495 2.53189ZM12.4835 3.6786C12.2042 3.44307 11.7958 3.44307 11.5165 3.6786L4.76651 9.37071C4.59752 9.51321 4.5 9.72301 4.5 9.94406L4.5 19.2539C4.5 19.392 4.61193 19.5039 4.75 19.5039H8.25C8.38807 19.5039 8.49999 19.392 8.49999 19.2539L8.49999 14.2468C8.49999 13.2803 9.2835 12.4968 10.25 12.4968H13.75C14.7165 12.4968 15.5 13.2803 15.5 14.2468L15.5 19.2539C15.5 19.392 15.6119 19.5039 15.75 19.5039H19.25C19.3881 19.5039 19.5 19.392 19.5 19.2539L19.5 9.94406C19.5 9.72301 19.4025 9.51321 19.2335 9.37071L12.4835 3.6786Z";

    private const string AppsGeometry =
        "M18.4923 2.33088L21.671 5.50966C22.5497 6.38834 22.5497 7.81296 21.671 8.69164L19.0866 11.2756C20.1696 11.438 21 12.3723 21 13.5006V18.7506C21 19.9932 19.9926 21.0006 18.75 21.0006H5.25C4.00736 21.0006 3 19.9932 3 18.7506V5.25055C3 4.00791 4.00736 3.00055 5.25 3.00055H10.5C11.6289 3.00055 12.5637 3.83201 12.7253 4.91596L15.3103 2.33088C16.189 1.45221 17.6136 1.45221 18.4923 2.33088ZM4.5 18.7506C4.5 19.1648 4.83579 19.5006 5.25 19.5006L11.249 19.4999L11.25 12.7506L4.5 12.7499V18.7506ZM12.749 19.4999L18.75 19.5006C19.1642 19.5006 19.5 19.1648 19.5 18.7506V13.5006C19.5 13.0863 19.1642 12.7506 18.75 12.7506L12.749 12.7499V19.4999ZM10.5 4.50055H5.25C4.83579 4.50055 4.5 4.83634 4.5 5.25055V11.2499H11.25V5.25055C11.25 4.83634 10.9142 4.50055 10.5 4.50055ZM12.75 9.30988V11.2506L14.69 11.2499L12.75 9.30988ZM16.3709 3.39154L13.1922 6.57032C12.8993 6.86321 12.8993 7.33808 13.1922 7.63098L16.3709 10.8097C16.6638 11.1026 17.1387 11.1026 17.4316 10.8097L20.6104 7.63098C20.9033 7.33808 20.9033 6.86321 20.6104 6.57032L17.4316 3.39154C17.1387 3.09865 16.6638 3.09865 16.3709 3.39154Z";

    private const string WindowConsoleGeometry =
        "M5.64645 9.14645C5.84171 8.95118 6.15829 8.95118 6.35355 9.14645L8.35355 11.1464C8.44732 11.2402 8.5 11.3674 8.5 11.5C8.5 11.6326 8.44732 11.7598 8.35355 11.8536L6.35355 13.8536C6.15829 14.0488 5.84171 14.0488 5.64645 13.8536C5.45118 13.6583 5.45118 13.3417 5.64645 13.1464L7.29289 11.5L5.64645 9.85355C5.45118 9.65829 5.45118 9.34171 5.64645 9.14645ZM14.5 13H9.5C9.22386 13 9 13.2239 9 13.5C9 13.7761 9.22386 14 9.5 14H14.5C14.7761 14 15 13.7761 15 13.5C15 13.2239 14.7761 13 14.5 13ZM2.99609 5.5C2.99609 4.11929 4.11538 3 5.49609 3H14.4961C15.8768 3 16.9961 4.11929 16.9961 5.5V6H16.999V7H16.9961V14.5C16.9961 15.8807 15.8768 17 14.4961 17H5.49609C4.11538 17 2.99609 15.8807 2.99609 14.5V5.5ZM15.9961 6V5.5C15.9961 4.67157 15.3245 4 14.4961 4H5.49609C4.66767 4 3.99609 4.67157 3.99609 5.5V6H15.9961ZM3.99609 7V14.5C3.99609 15.3284 4.66767 16 5.49609 16H14.4961C15.3245 16 15.9961 15.3284 15.9961 14.5V7H3.99609Z";

    private const string PulseGeometry =
        "M8.46238 6.80905L11.746 20.426C11.9236 21.1626 12.957 21.2011 13.1891 20.4798L16.4456 10.3575L17.0318 12.4532C17.1224 12.7772 17.4176 13.0012 17.7541 13.0012H21.2477C21.6619 13.0012 21.9977 12.6654 21.9977 12.2512C21.9977 11.837 21.6619 11.5012 21.2477 11.5012H18.3231L17.2181 7.55053C17.0179 6.83439 16.0096 6.81496 15.7819 7.52284L12.5785 17.4797L9.22531 3.57419C9.04279 2.81728 7.97039 2.80542 7.77117 3.5581L5.66883 11.5012H2.75C2.33579 11.5012 2 11.837 2 12.2512C2 12.6654 2.33579 13.0012 2.75 13.0012H6.24614C6.58645 13.0012 6.88411 12.7721 6.97118 12.4431L8.46238 6.80905Z";

    private const string BracesVariableGeometry =
        "M3.5 5.75C3.5 4.23122 4.73122 3 6.25 3C6.66421 3 7 3.33579 7 3.75C7 4.16421 6.66421 4.5 6.25 4.5C5.55964 4.5 5 5.05964 5 5.75V10.0585C5 10.8034 4.69999 11.4958 4.19767 12C4.69999 12.5042 5 13.1966 5 13.9415V18.25C5 18.9404 5.55964 19.5 6.25 19.5C6.66421 19.5 7 19.8358 7 20.25C7 20.6642 6.66421 21 6.25 21C4.73122 21 3.5 19.7688 3.5 18.25V13.9415C3.5 13.4035 3.15571 12.9258 2.64528 12.7557L2.51283 12.7115C2.20657 12.6094 2 12.3228 2 12C2 11.6772 2.20657 11.3906 2.51283 11.2885L2.64528 11.2443C3.15571 11.0742 3.5 10.5965 3.5 10.0585V5.75ZM20.5 5.75C20.5 4.23122 19.2688 3 17.75 3C17.3358 3 17 3.33579 17 3.75C17 4.16421 17.3358 4.5 17.75 4.5C18.4404 4.5 19 5.05964 19 5.75V10.0585C19 10.8034 19.3 11.4958 19.8023 12C19.3 12.5042 19 13.1966 19 13.9415V18.25C19 18.9404 18.4404 19.5 17.75 19.5C17.3358 19.5 17 19.8358 17 20.25C17 20.6642 17.3358 21 17.75 21C19.2688 21 20.5 19.7688 20.5 18.25V13.9415C20.5 13.4035 20.8443 12.9258 21.3547 12.7557L21.4872 12.7115C21.7934 12.6094 22 12.3228 22 12C22 11.6772 21.7934 11.3906 21.4872 11.2885L21.3547 11.2443C20.8443 11.0742 20.5 10.5965 20.5 10.0585V5.75ZM9.09201 7.03954C8.83771 6.71258 8.36651 6.65368 8.03954 6.90799C7.71258 7.16229 7.65368 7.63349 7.90799 7.96046L11.0499 12L7.90799 16.0395C7.65368 16.3665 7.71258 16.8377 8.03954 17.092C8.36651 17.3463 8.83771 17.2874 9.09201 16.9605L12 13.2216L14.908 16.9605C15.1623 17.2874 15.6335 17.3463 15.9605 17.092C16.2874 16.8377 16.3463 16.3665 16.092 16.0395L12.9501 12L16.092 7.96046C16.3463 7.63349 16.2874 7.16229 15.9605 6.90799C15.6335 6.65368 15.1623 6.71258 14.908 7.03954L12 10.7784L9.09201 7.03954Z";

    private const string SettingsGeometry =
        "M12.0122 2.25C12.7462 2.25846 13.4773 2.34326 14.1937 2.50304C14.5064 2.57279 14.7403 2.83351 14.7758 3.15196L14.946 4.67881C15.0231 5.37986 15.615 5.91084 16.3206 5.91158C16.5103 5.91188 16.6979 5.87238 16.8732 5.79483L18.2738 5.17956C18.5651 5.05159 18.9055 5.12136 19.1229 5.35362C20.1351 6.43464 20.8889 7.73115 21.3277 9.14558C21.4223 9.45058 21.3134 9.78203 21.0564 9.9715L19.8149 10.8866C19.4607 11.1468 19.2516 11.56 19.2516 11.9995C19.2516 12.4389 19.4607 12.8521 19.8157 13.1129L21.0582 14.0283C21.3153 14.2177 21.4243 14.5492 21.3297 14.8543C20.8911 16.2685 20.1377 17.5649 19.1261 18.6461C18.9089 18.8783 18.5688 18.9483 18.2775 18.8206L16.8712 18.2045C16.4688 18.0284 16.0068 18.0542 15.6265 18.274C15.2463 18.4937 14.9933 18.8812 14.945 19.3177L14.7759 20.8444C14.741 21.1592 14.5122 21.4182 14.204 21.4915C12.7556 21.8361 11.2465 21.8361 9.79803 21.4915C9.48991 21.4182 9.26105 21.1592 9.22618 20.8444L9.05736 19.32C9.00777 18.8843 8.75434 18.498 8.37442 18.279C7.99451 18.06 7.5332 18.0343 7.1322 18.2094L5.72557 18.8256C5.43422 18.9533 5.09403 18.8833 4.87678 18.6509C3.86462 17.5685 3.11119 16.2705 2.6732 14.8548C2.57886 14.5499 2.68786 14.2186 2.94485 14.0293L4.18818 13.1133C4.54232 12.8531 4.75147 12.4399 4.75147 12.0005C4.75147 11.561 4.54232 11.1478 4.18771 10.8873L2.94516 9.97285C2.6878 9.78345 2.5787 9.45178 2.67337 9.14658C3.11212 7.73215 3.86594 6.43564 4.87813 5.35462C5.09559 5.12236 5.43594 5.05259 5.72724 5.18056L7.12762 5.79572C7.53056 5.97256 7.9938 5.94585 8.37577 5.72269C8.75609 5.50209 9.00929 5.11422 9.05817 4.67764L9.22824 3.15196C9.26376 2.83335 9.49786 2.57254 9.8108 2.50294C10.5281 2.34342 11.26 2.25865 12.0122 2.25ZM12.0124 3.7499C11.5583 3.75524 11.1056 3.79443 10.6578 3.86702L10.5489 4.84418C10.4471 5.75368 9.92003 6.56102 9.13042 7.01903C8.33597 7.48317 7.36736 7.53903 6.52458 7.16917L5.62629 6.77456C5.05436 7.46873 4.59914 8.25135 4.27852 9.09168L5.07632 9.67879C5.81513 10.2216 6.25147 11.0837 6.25147 12.0005C6.25147 12.9172 5.81513 13.7793 5.0771 14.3215L4.27805 14.9102C4.59839 15.752 5.05368 16.5361 5.626 17.2316L6.53113 16.8351C7.36923 16.4692 8.33124 16.5227 9.12353 16.9794C9.91581 17.4361 10.4443 18.2417 10.548 19.1526L10.657 20.1365C11.5466 20.2878 12.4555 20.2878 13.3451 20.1365L13.4541 19.1527C13.5549 18.2421 14.0828 17.4337 14.876 16.9753C15.6692 16.5168 16.6332 16.463 17.4728 16.8305L18.3772 17.2267C18.949 16.5323 19.4041 15.7495 19.7247 14.909L18.9267 14.3211C18.1879 13.7783 17.7516 12.9162 17.7516 11.9995C17.7516 11.0827 18.1879 10.2206 18.9258 9.67847L19.7227 9.09109C19.4021 8.25061 18.9468 7.46784 18.3748 6.77356L17.4783 7.16737C17.113 7.32901 16.7178 7.4122 16.3187 7.41158C14.849 7.41004 13.6155 6.30355 13.4551 4.84383L13.3462 3.8667C12.9007 3.7942 12.4526 3.75512 12.0124 3.7499ZM11.9997 8.24995C14.0708 8.24995 15.7497 9.92888 15.7497 12C15.7497 14.071 14.0708 15.75 11.9997 15.75C9.92863 15.75 8.2497 14.071 8.2497 12C8.2497 9.92888 9.92863 8.24995 11.9997 8.24995ZM11.9997 9.74995C10.7571 9.74995 9.7497 10.7573 9.7497 12C9.7497 13.2426 10.7571 14.25 11.9997 14.25C13.2423 14.25 14.2497 13.2426 14.2497 12C14.2497 10.7573 13.2423 9.74995 11.9997 9.74995Z";

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
            IconSource = CreateNavIcon(HomeGeometry),
            Tag = typeof(DashboardViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Projects"),
            IconSource = CreateNavIcon(AppsGeometry),
            Tag = typeof(ProjectsViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Terminal"),
            IconSource = CreateNavIcon(WindowConsoleGeometry),
            Tag = typeof(TerminalViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Performance"),
            IconSource = CreateNavIcon(PulseGeometry),
            Tag = typeof(PerformanceViewModel)
        });
        _allMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Environment"),
            IconSource = CreateNavIcon(BracesVariableGeometry),
            Tag = typeof(SystemEnvironmentVariablesViewModel)
        });

        _allFooterMenuItems.Add(new FANavigationViewItem
        {
            Content = _languageService.GetString("Nav_Settings"),
            IconSource = CreateNavIcon(SettingsGeometry),
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
            "EnvironmentVariables" => typeof(SystemEnvironmentVariablesViewModel),
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

        // Same-page navigation: still trigger lifecycle to process pending state
        // (e.g., clicking a project's Terminal button while already on the Terminal page
        //  should switch to that project's terminal tab instead of doing nothing)
        if (ReferenceEquals(CurrentPage, vm))
        {
            var onToAsync = vm.GetType().GetMethod("OnNavigatedToAsync");
            if (onToAsync != null)
            {
                onToAsync.Invoke(vm, null);
            }
            else
            {
                vm.GetType().GetMethod("OnNavigatedTo")?.Invoke(vm, null);
            }
            return;
        }

        // Call OnNavigatedFrom on the previous page
        var oldPage = CurrentPage;
        if (oldPage != null)
        {
            var onFrom = oldPage.GetType().GetMethod("OnNavigatedFrom");
            onFrom?.Invoke(oldPage, null);
        }

        // Guard: prevent HandleNavigationSelectionChanged from double-triggering lifecycle
        // when SelectedItem change synchronously fires the SelectionChanged event
        _isNavigatingProgrammatically = true;
        try
        {
            SelectedItem = item;
            CurrentPage = vm;
        }
        finally
        {
            _isNavigatingProgrammatically = false;
        }

        // Call OnNavigatedTo/OnNavigatedToAsync on the new page
        var onToAsync2 = vm.GetType().GetMethod("OnNavigatedToAsync");
        if (onToAsync2 != null)
        {
            onToAsync2.Invoke(vm, null);
        }
        else
        {
            vm.GetType().GetMethod("OnNavigatedTo")?.Invoke(vm, null);
        }
    }

    public void HandleNavigationSelectionChanged(FANavigationViewSelectionChangedEventArgs? args)
    {
        // Skip if this SelectionChanged was triggered by programmatic navigation
        // (NavigateToItem will handle lifecycle methods itself)
        if (_isNavigatingProgrammatically) return;

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
