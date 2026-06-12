using Avalonia.Controls;
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

        // 初始导航到默认页面
        if (DataContext is MainWindowViewModel vm)
        {
            vm.NavigateToDefault();
        }
    }

    private void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.HandleNavigationSelectionChanged(e);
        }
    }
}
