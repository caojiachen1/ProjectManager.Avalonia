using System.Text.Json.Serialization;

namespace ProjectManager.Avalonia.Models;

/// <summary>
/// 仅用于磁盘持久化的精简项目 DTO，避免序列化运行时对象(Process/GitInfo等)。
/// </summary>
public class PersistedProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public ProjectStatus Status { get; set; } = ProjectStatus.Stopped; // 读取时会重置为 Stopped
    public int? ProcessId { get; set; }
    public string LogOutput { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool AutoStart { get; set; }
    public Dictionary<string,string> EnvironmentVariables { get; set; } = new();
    public List<string> GitRepositories { get; set; } = new();
    public ComfyUISettings? ComfyUISettings { get; set; }
    public NodeJSSettings? NodeJSSettings { get; set; }
    public DotNetSettings? DotNetSettings { get; set; }
}

internal static class ProjectPersistenceMapper
{
    public static PersistedProject ToDto(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        LocalPath = p.LocalPath,
        StartCommand = p.StartCommand,
        WorkingDirectory = p.WorkingDirectory,
        Framework = p.Framework,
        CreatedDate = p.CreatedDate,
        LastModified = p.LastModified,
        Status = p.Status, // 保存当前状态（读取时会安全处理）
        ProcessId = p.ProcessId,
        LogOutput = p.LogOutput,
        Tags = new List<string>(p.Tags),
        AutoStart = p.AutoStart,
        EnvironmentVariables = new Dictionary<string,string>(p.EnvironmentVariables),
        GitRepositories = new List<string>(p.GitRepositories),
        ComfyUISettings = p.ComfyUISettings == null ? null : p.ComfyUISettings,
        NodeJSSettings = p.NodeJSSettings == null ? null : p.NodeJSSettings,
        DotNetSettings = p.DotNetSettings == null ? null : p.DotNetSettings
    };

    public static Project ToModel(PersistedProject dto)
    {
        var model = new Project
        {
            Id = dto.Id ?? Guid.NewGuid().ToString(),
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            LocalPath = dto.LocalPath ?? string.Empty,
            StartCommand = dto.StartCommand ?? string.Empty,
            WorkingDirectory = dto.WorkingDirectory ?? string.Empty,
            Framework = dto.Framework ?? string.Empty,
            CreatedDate = dto.CreatedDate,
            LastModified = dto.LastModified,
            Status = dto.Status, // 尝试恢复状态，后续会验证
            ProcessId = dto.ProcessId,
            LogOutput = dto.LogOutput ?? string.Empty,
            Tags = new List<string>(dto.Tags ?? new()),
            AutoStart = dto.AutoStart,
            EnvironmentVariables = dto.EnvironmentVariables != null 
                ? new Dictionary<string,string>(dto.EnvironmentVariables) 
                : new Dictionary<string,string>(),
            GitRepositories = new List<string>(dto.GitRepositories ?? new())
        };
        if (dto.ComfyUISettings != null)
        {
            model.ComfyUISettings = dto.ComfyUISettings;
        }
        if (dto.NodeJSSettings != null)
        {
            model.NodeJSSettings = dto.NodeJSSettings;
        }
        if (dto.DotNetSettings != null)
        {
            model.DotNetSettings = dto.DotNetSettings;
        }
        return model;
    }
}
