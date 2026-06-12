using System;

namespace ProjectManager.Avalonia.Models
{
    public class ProjectPerformanceSnapshot
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public ProjectStatus Status { get; set; } = ProjectStatus.Stopped;
        public string StatusDisplay { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        // 系统总物理内存（MB）
        public double TotalMemoryMb { get; set; }
        public double CpuUsagePercent { get; set; }
        public double? GpuUsagePercent { get; set; }
        public double MemoryUsageMb { get; set; }
        public double PrivateMemoryUsageMb { get; set; }
        public int ThreadCount { get; set; }
        public long? ProcessId { get; set; }
        public string? ProcessName { get; set; }
        public DateTime? ProcessStartTime { get; set; }
        public TimeSpan? Uptime { get; set; }
        public string LocalPath { get; set; } = string.Empty;
        public string StartCommand { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; } = DateTime.Now;
        public bool IsRunning => Status == ProjectStatus.Running;
    }
}
