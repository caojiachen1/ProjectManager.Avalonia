using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Avalonia.ViewModels.Dialogs;

namespace ProjectManager.Avalonia.Views.Dialogs;

public partial class NewProjectWindow : Window
{
    public NewProjectWindow()
    {
        InitializeComponent();
    }

    public NewProjectWindow(NewProjectDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        SubscribeToEvents(viewModel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is NewProjectDialogViewModel vm)
        {
            SubscribeToEvents(vm);
        }
    }

    private void SubscribeToEvents(NewProjectDialogViewModel vm)
    {
        vm.ProjectCreated += (_, _) => Close(true);
        vm.DialogCancelled += (_, _) => Close(false);
    }
}
