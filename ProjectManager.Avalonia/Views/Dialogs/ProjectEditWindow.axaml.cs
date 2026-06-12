using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class ProjectEditWindow : Window
{
    public ProjectEditWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ProjectEditDialogViewModel vm)
        {
            vm.ProjectSaved += (_, _) => Close(true);
            vm.ProjectDeleted += (_, _) => Close(true);
            vm.DialogCancelled += (_, _) => Close(false);
        }
    }

    private void FrameworkCommand_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is string command)
        {
            if (DataContext is ProjectEditDialogViewModel vm)
            {
                vm.ApplyFrameworkCommandCommand.Execute(command);
                comboBox.SelectedIndex = -1;
            }
        }
    }
}
