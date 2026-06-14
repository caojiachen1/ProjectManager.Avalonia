using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class PathEditorWindow : Window
{
    private PathEditorViewModel? _viewModel;

    public PathEditorWindow()
    {
        InitializeComponent();
    }

    public PathEditorWindow(PathEditorViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += (sender, result) =>
        {
            Close(result);
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel = DataContext as PathEditorViewModel;
    }

    private void PathDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel?.SelectedPathItem != null)
        {
            _viewModel.EditCommand.Execute(null);
        }
    }

    private void PathDataGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.Delete:
                if (_viewModel.SelectedPathItem != null)
                    _viewModel.DeleteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                if (_viewModel.SelectedPathItem != null)
                    _viewModel.EditCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Insert:
                _viewModel.NewCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F2:
                if (_viewModel.SelectedPathItem != null)
                    _viewModel.EditCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up when e.KeyModifiers == KeyModifiers.Alt:
                if (_viewModel.CanMoveUp)
                {
                    _viewModel.MoveUpCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Down when e.KeyModifiers == KeyModifiers.Alt:
                if (_viewModel.CanMoveDown)
                {
                    _viewModel.MoveDownCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
