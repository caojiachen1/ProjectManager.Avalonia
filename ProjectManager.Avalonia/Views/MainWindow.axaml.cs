using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using ProjectManager.Avalonia.ViewModels;

namespace ProjectManager.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 初始导航到默认启动页面（根据设置）
        if (DataContext is MainWindowViewModel vm)
        {
            _ = vm.NavigateToDefaultAsync();
        }
    }

    private void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.HandleNavigationSelectionChanged(e);
        }
    }

    private void OnContentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var searchBox = this.FindControl<TextBox>("NavSearchBox");
        if (searchBox == null || !searchBox.IsFocused) return;

        var pos = e.GetCurrentPoint(this).Position;
        var hit = this.InputHitTest(pos) as Control;

        Control? current = hit;
        while (current != null)
        {
            if (current == searchBox) return;
            current = current.Parent as Control;
        }

        RootNavigation.Focus();
    }
}
