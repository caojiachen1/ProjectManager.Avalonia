using Avalonia.Controls;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

public interface IProjectSettingsWindowService
{
    Task<bool?> ShowSettingsWindowAsync(Project project, Window owner);
}
