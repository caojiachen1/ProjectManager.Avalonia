using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class DotNetProjectSettingsWindow : Window
{
    public DotNetProjectSettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is DotNetProjectSettingsViewModel vm)
        {
            vm.ProjectSaved += (_, _) => Close(true);
            vm.ProjectDeleted += (_, _) => Close(true);
            vm.DialogCancelled += (_, _) => Close(false);
        }
    }
}
