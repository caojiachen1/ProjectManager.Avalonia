using Avalonia.Controls;
using Avalonia.Input;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.Views.Pages;

public partial class SystemEnvironmentVariablesPage : UserControl
{
    public SystemEnvironmentVariablesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SystemEnvironmentVariablesViewModel vm)
        {
            await vm.EnsureInitializedAsync();
        }
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            var point = e.GetCurrentPoint(dataGrid);
            var hit = dataGrid.InputHitTest(point.Position) as Control;
            // Walk up the visual tree from hit element
            Control? current = hit;
            bool foundRow = false;
            while (current != null && current != dataGrid)
            {
                if (current is DataGridRow)
                {
                    foundRow = true;
                    break;
                }
                current = current.Parent as Control;
            }

            if (!foundRow)
            {
                // Clicked on blank area, deselect
                if (DataContext is SystemEnvironmentVariablesViewModel vm)
                {
                    if (dataGrid.Name == "UserDataGrid")
                        vm.SelectedUserVariable = null;
                    else if (dataGrid.Name == "SystemDataGrid")
                        vm.SelectedSystemVariable = null;
                }
            }
        }
    }

    private void OnDataGridDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && DataContext is SystemEnvironmentVariablesViewModel vm)
        {
            // Only open edit if double-clicked on a row
            var point = e.GetPosition(dataGrid);
            var hit = dataGrid.InputHitTest(point) as Control;
            Control? current = hit;
            bool foundRow = false;
            while (current != null && current != dataGrid)
            {
                if (current is DataGridRow)
                {
                    foundRow = true;
                    break;
                }
                current = current.Parent as Control;
            }

            if (foundRow)
            {
                if (dataGrid.Name == "UserDataGrid" && vm.EditUserVariableCommand.CanExecute(null))
                    _ = vm.EditUserVariableCommand.ExecuteAsync(null);
                else if (dataGrid.Name == "SystemDataGrid" && vm.EditSystemVariableCommand.CanExecute(null))
                    _ = vm.EditSystemVariableCommand.ExecuteAsync(null);
            }
        }
    }
}
