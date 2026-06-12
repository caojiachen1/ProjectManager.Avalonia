using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class EditEnvironmentVariableWindow : Window
{
    public EditEnvironmentVariableWindow()
    {
        InitializeComponent();
    }

    public EditEnvironmentVariableWindow(EditEnvironmentVariableViewModel viewModel) : this()
    {
        DataContext = viewModel;
        SubscribeToEvents(viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is EditEnvironmentVariableViewModel vm)
        {
            SubscribeToEvents(vm);
        }
    }

    private void SubscribeToEvents(EditEnvironmentVariableViewModel vm)
    {
        vm.SaveCompleted += OnSaveCompleted;
        vm.PathEditRequested += OnPathEditRequested;
    }

    private void OnSaveCompleted(object? sender, bool success)
    {
        if (success)
            Close(true);
    }

    private async void OnPathEditRequested(object? sender, string pathValue)
    {
        if (DataContext is EditEnvironmentVariableViewModel vm)
        {
            var pathVm = new PathEditorViewModel(pathValue, vm.IsSystemVariable);
            var pathWindow = new PathEditorWindow(pathVm);
            if (await pathWindow.ShowDialog<bool?>(this) == true)
            {
                // Update the variable value with the result from path editor
                vm.VariableValue = pathVm.ResultValue;
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
