using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using ProjectManager.Avalonia.ViewModels;

namespace ProjectManager.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 全局点击空白处取消输入框聚焦（Tunnel+Bubble 双策略，handledEventsToo 确保捕获所有事件）
        AddHandler(PointerPressedEvent, OnGlobalPointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is MainWindowViewModel vm)
        {
            _ = vm.NavigateToDefaultAsync();
        }

        RootNavigation.PaneOpened += OnPaneOpened;
        RootNavigation.PaneClosed += OnPaneClosed;

        // 搜索框宽度绑定到导航面板的 OpenPaneLength，防止输入文字时搜索框缩小
        // SearchGrid 的 Margin 是 8,4,8,4，所以宽度要减去左右 Margin 各 8
        UpdateSearchGridWidth();
        RootNavigation.PropertyChanged += OnRootNavigationPropertyChanged;
    }

    private void OnRootNavigationPropertyChanged(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FANavigationView.OpenPaneLengthProperty)
        {
            UpdateSearchGridWidth();
        }
    }

    private void UpdateSearchGridWidth()
    {
        SearchGrid.Width = RootNavigation.OpenPaneLength - 16;
    }

    private void OnGlobalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 从事件源向上遍历视觉树，检查是否点击在 TextBox 内部
        var source = e.Source as Visual;
        var current = source;
        while (current != null)
        {
            if (current is TextBox) return; // 点击的是 TextBox，保留焦点
            current = current.GetVisualParent();
        }

        // 点击的不是 TextBox，延迟移除焦点（确保在当前事件处理完成后执行，避免被覆盖）
        Dispatcher.UIThread.Post(() =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var fm = topLevel.FocusManager;
            if (fm?.GetFocusedElement() is not TextBox) return;

            // 从点击源向上寻找可聚焦的祖先元素
            Visual? ancestor = source?.GetVisualParent();
            InputElement? target = null;
            while (ancestor != null)
            {
                if (ancestor is TextBox) break; // 不能聚焦 TextBox
                if (ancestor is InputElement ie && ie.Focusable)
                {
                    target = ie;
                    break;
                }
                ancestor = ancestor.GetVisualParent();
            }

            // 如果没有找到原生可聚焦的元素，临时启用最近祖先的 Focusable
            if (target == null && source != null)
            {
                var fallback = source.GetVisualParent() as InputElement;
                if (fallback != null)
                {
                    fallback.Focusable = true;
                    target = fallback;
                }
            }

            if (target != null)
            {
                fm.Focus(target, NavigationMethod.Pointer);
            }
        }, DispatcherPriority.Input);
    }

    private void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.HandleNavigationSelectionChanged(e);
        }
    }

    private void OnPaneOpened(FANavigationView sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.IsPaneOpen = true;
    }

    private void OnPaneClosed(FANavigationView sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.IsPaneOpen = false;
    }

    private void OnCollapsedSearchClick(object? sender, RoutedEventArgs e)
    {
        RootNavigation.IsPaneOpen = true;
        if (DataContext is MainWindowViewModel vm)
            vm.IsPaneOpen = true;

        var searchBox = this.FindControl<TextBox>("NavSearchBox");
        searchBox?.Focus();
    }
}
