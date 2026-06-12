using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class GitCloneWindow : Window
{
    public GitCloneWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is GitCloneDialogViewModel vm)
        {
            vm.CloneCompleted += OnCloneCompleted;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (DataContext is GitCloneDialogViewModel vm)
        {
            vm.CloneCompleted -= OnCloneCompleted;
        }
    }

    private void OnCloneCompleted(object? sender, bool result)
    {
        Close(result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
