using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.ViewModels.Dialogs;
using ProjectManager.Avalonia.Views.Dialogs;

namespace ProjectManager.Avalonia.Services;

public class ProjectSettingsWindowService : IProjectSettingsWindowService
{
    private readonly IServiceProvider _serviceProvider;

    public ProjectSettingsWindowService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool?> ShowSettingsWindowAsync(Project project, Window owner)
    {
        Window? dialog = project.Framework?.ToLower() switch
        {
            "comfyui" => CreateComfyUIDialog(project),
            "node.js" => CreateNodeJSDialog(project),
            ".net" => CreateDotNetDialog(project),
            _ => CreateGenericDialog(project)
        };

        if (dialog != null)
        {
            return await dialog.ShowDialog<bool?>(owner);
        }
        return null;
    }

    private Window CreateComfyUIDialog(Project project)
    {
        var vm = _serviceProvider.GetRequiredService<ComfyUIProjectSettingsViewModel>();
        vm.LoadProject(project);
        var window = _serviceProvider.GetRequiredService<ComfyUIProjectSettingsWindow>();
        window.DataContext = vm;
        return window;
    }

    private Window CreateNodeJSDialog(Project project)
    {
        var vm = _serviceProvider.GetRequiredService<NodeJSProjectSettingsViewModel>();
        vm.LoadProject(project);
        var window = _serviceProvider.GetRequiredService<NodeJSProjectSettingsWindow>();
        window.DataContext = vm;
        return window;
    }

    private Window CreateDotNetDialog(Project project)
    {
        var vm = _serviceProvider.GetRequiredService<DotNetProjectSettingsViewModel>();
        vm.LoadProject(project);
        var window = _serviceProvider.GetRequiredService<DotNetProjectSettingsWindow>();
        window.DataContext = vm;
        return window;
    }

    private Window CreateGenericDialog(Project project)
    {
        var vm = _serviceProvider.GetRequiredService<GenericProjectSettingsViewModel>();
        vm.LoadProject(project);
        var window = _serviceProvider.GetRequiredService<GenericProjectSettingsWindow>();
        window.DataContext = vm;
        return window;
    }
}
