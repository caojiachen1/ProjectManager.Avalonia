using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class GitManagementWindow : Window
{
    public GitManagementWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is GitManagementDialogViewModel vm)
        {
            vm.GitInfoUpdated += OnGitInfoUpdated;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (DataContext is GitManagementDialogViewModel vm)
        {
            vm.GitInfoUpdated -= OnGitInfoUpdated;
        }
    }

    private void OnGitInfoUpdated(object? sender, ProjectManager.Avalonia.Models.Project e)
    {
        // Informational event - no action needed in the dialog.
        // The VM already updates GitInfo via the refresh flow.
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
