using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class EnvironmentVariablesWindow : Window
{
    public EnvironmentVariablesWindow()
    {
        InitializeComponent();
    }

    public EnvironmentVariablesWindow(EnvironmentVariablesDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EnvironmentVariablesDialogViewModel vm)
        {
            vm.SaveChanges();
        }
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
