using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class PathItemEditWindow : Window
{
    public PathItemEditWindow()
    {
        InitializeComponent();
    }

    public PathItemEditWindow(PathItemEditViewModel viewModel) : this()
    {
        DataContext = viewModel;
        if (viewModel != null)
        {
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PathItemEditViewModel vm)
        {
            vm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        Close(result);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PathItemEditViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Path))
                return;
        }
        Close(true);
    }
}
