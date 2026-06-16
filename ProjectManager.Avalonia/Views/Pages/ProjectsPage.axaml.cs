using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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

    /// <summary>
    /// The project item that the 3-dot menu button belongs to.
    /// Set by <see cref="MenuButton_Click"/> before the flyout opens.
    /// </summary>
    private Project? _menuTargetProject;

    /// <summary>
    /// Records the project item that the user clicked the 3-dot menu on.
    /// </summary>
    private void MenuButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Project project })
            _menuTargetProject = project;
    }

    private void MenuOpenInExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (_menuTargetProject != null)
            ViewModel?.OpenProjectInExplorerCommand.Execute(_menuTargetProject);
    }

    private void MenuDeleteProject_Click(object? sender, RoutedEventArgs e)
    {
        if (_menuTargetProject != null)
            ViewModel?.DeleteProjectCommand.Execute(_menuTargetProject);
    }

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
