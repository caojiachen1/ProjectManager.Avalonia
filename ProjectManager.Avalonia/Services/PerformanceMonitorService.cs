using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Cross-platform performance monitor service.
/// Tracks per-project CPU, memory, thread count, and uptime.
/// Uses kernel32 GlobalMemoryStatusEx on Windows, /proc/meminfo on Linux, sysctl on macOS.
/// </summary>
public class PerformanceMonitorService : IPerformanceMonitorService
{
    private readonly IProjectService _projectService;
    private readonly Dictionary<int, CpuSample> _cpuSamples = new();
    private readonly object _cpuLock = new();
    private readonly SemaphoreSlim _processQuerySemaphore = new(4);
    private double? _cachedTotalMemory;
    private DateTime _lastMemoryCheck = DateTime.MinValue;

    public PerformanceMonitorService(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<IReadOnlyList<ProjectPerformanceSnapshot>> GetProjectPerformanceAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _projectService.GetProjectsAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.Now;

        var tasks = projects.Select(async project =>
        {
            await _processQuerySemaphore.WaitAsync(cancellationToken);
            try
            {
                return CreateSnapshot(project, now);
            }
            finally
            {
                _processQuerySemaphore.Release();
            }
        });

        var snapshots = await Task.WhenAll(tasks);
        return snapshots;
    }

    private ProjectPerformanceSnapshot CreateSnapshot(Project project, DateTime capturedAt)
    {
        Process? process = ResolveProcessForSnapshot(project);

        var snapshot = new ProjectPerformanceSnapshot
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Status = project.Status,
            StatusDisplay = project.StatusDisplay,
            Framework = project.Framework,
            LocalPath = project.LocalPath,
            StartCommand = project.StartCommand,
            CapturedAt = capturedAt
        };

        if (process == null)
        {
            return snapshot;
        }

        snapshot.ProcessId = SafeGet<long?>(() => process.Id);
        snapshot.ProcessName = SafeGet(() => process.ProcessName);
        if (!TryPopulateAggregatedMemory(process, snapshot))
        {
            snapshot.MemoryUsageMb = SafeGet(() => process.WorkingSet64 / 1024d / 1024d);
            snapshot.PrivateMemoryUsageMb = SafeGet(() => process.PrivateMemorySize64 / 1024d / 1024d);
        }
        snapshot.ThreadCount = SafeGet(() => process.Threads.Count);
        snapshot.ProcessStartTime = SafeGet<DateTime?>(() => process.StartTime);
        snapshot.Uptime = snapshot.ProcessStartTime.HasValue ? capturedAt - snapshot.ProcessStartTime.Value : null;
        snapshot.CpuUsagePercent = CalculateCpuUsage(process);
        snapshot.TotalMemoryMb = SafeGet(() => GetTotalPhysicalMemoryMb());

        return snapshot;
    }

    private double CalculateCpuUsage(Process process)
    {
        try
        {
            var now = DateTime.UtcNow;
            var totalProcessorTime = process.TotalProcessorTime;

            lock (_cpuLock)
            {
                if (_cpuSamples.TryGetValue(process.Id, out var sample))
                {
                    var cpuDelta = (totalProcessorTime - sample.TotalProcessorTime).TotalMilliseconds;
                    var timeDelta = (now - sample.Timestamp).TotalMilliseconds;
                    _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);

                    if (timeDelta <= 0)
                        return 0d;

                    var usage = cpuDelta / (Environment.ProcessorCount * timeDelta) * 100d;
                    if (double.IsNaN(usage) || double.IsInfinity(usage))
                        return 0d;
                    return Math.Clamp(usage, 0d, 100d);
                }

                _cpuSamples[process.Id] = new CpuSample(totalProcessorTime, now);
                return 0d;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to calculate CPU usage: {ex.Message}");
            RemoveCpuSample(process.Id);
            return 0d;
        }
    }

    private double GetTotalPhysicalMemoryMb()
    {
        var now = DateTime.Now;
        if (_cachedTotalMemory.HasValue && (now - _lastMemoryCheck).TotalSeconds < 10)
        {
            return _cachedTotalMemory.Value;
        }

        try
        {
            double totalMb = 0;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                totalMb = GetTotalPhysicalMemoryWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                totalMb = GetTotalPhysicalMemoryLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                totalMb = GetTotalPhysicalMemoryMacOS();
            }

            if (totalMb > 0)
            {
                _cachedTotalMemory = totalMb;
                _lastMemoryCheck = now;
            }

            return totalMb;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get total physical memory: {ex.Message}");
            return 0d;
        }
    }

    // ==================== Windows: kernel32 GlobalMemoryStatusEx ====================

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static double GetTotalPhysicalMemoryWindows()
    {
        var mem = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(mem))
        {
            return mem.ullTotalPhys / 1024d / 1024d;
        }
        return 0d;
    }

    // ==================== Linux: /proc/meminfo ====================

    private static double GetTotalPhysicalMemoryLinux()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:"))
                {
                    // Format: "MemTotal:       16384000 kB"
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var valueStr = parts[1].Trim().Replace("kB", "").Trim();
                        if (long.TryParse(valueStr, out long kbValue))
                        {
                            return kbValue / 1024d;
                        }
                    }
                }
            }
        }
        catch { }
        return 0d;
    }

    // ==================== macOS: sysctl ====================

    private static double GetTotalPhysicalMemoryMacOS()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sysctl",
                Arguments = "-n hw.memsize",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return 0d;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            if (long.TryParse(output, out long bytes))
            {
                return bytes / 1024d / 1024d;
            }
        }
        catch { }
        return 0d;
    }

    // ==================== Process Resolution ====================

    private Process? ResolveProcessForSnapshot(Project project)
    {
        Process? storedProcess = null;
        try
        {
            storedProcess = project.RunningProcess;
        }
        catch
        {
            storedProcess = null;
        }

        if (storedProcess == null)
            return null;

        Process? resolved = null;
        try
        {
            resolved = ProcessInterop.TryResolveRealProcess(storedProcess) ?? storedProcess;

            if (resolved == null)
                return null;

            if (resolved.Id != storedProcess.Id)
            {
                RemoveCpuSample(storedProcess.Id);
            }

            if (resolved.HasExited)
            {
                RemoveCpuSample(resolved.Id);
                return null;
            }
        }
        catch
        {
            return null;
        }

        return resolved;
    }

    private void RemoveCpuSample(int pid)
    {
        lock (_cpuLock)
        {
            _cpuSamples.Remove(pid);
        }
    }

    private bool TryPopulateAggregatedMemory(Process process, ProjectPerformanceSnapshot snapshot)
    {
        try
        {
            if (ProcessInterop.TryGetAggregatedMemoryUsage(process, out var workingMb, out var privateMb))
            {
                snapshot.MemoryUsageMb = workingMb;
                snapshot.PrivateMemoryUsageMb = privateMb;
                return true;
            }
        }
        catch { }

        return false;
    }

    private static T SafeGet<T>(Func<T> accessor)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return default!;
        }
    }

    private readonly record struct CpuSample(TimeSpan TotalProcessorTime, DateTime Timestamp);
}
