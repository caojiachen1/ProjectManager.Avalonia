using Avalonia.Controls;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.ViewModels.Pages;

namespace ProjectManager.Avalonia.Views.Pages;

public partial class ProjectsPage : UserControl
{
    public ProjectsPage()
    {
        InitializeComponent();
    }

    private ProjectsViewModel? ViewModel => DataContext as ProjectsViewModel;

    private void OpenInExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Project project })
            ViewModel?.OpenProjectInExplorerCommand.Execute(project);
    }

    private void OpenInVSCode_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Project project })
            ViewModel?.OpenProjectInVSCodeCommand.Execute(project);
    }

    private void DeleteProject_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Project project })
            ViewModel?.DeleteProjectCommand.Execute(project);
    }
}
