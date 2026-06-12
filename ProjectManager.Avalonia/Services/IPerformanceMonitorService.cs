using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Monitors system performance metrics (CPU, memory, threads) per project.
/// </summary>
public interface IPerformanceMonitorService
{
    /// <summary>
    /// Take a performance snapshot for all running projects.
    /// </summary>
    Task<IReadOnlyList<ProjectPerformanceSnapshot>> GetProjectPerformanceAsync(CancellationToken cancellationToken = default);
}
