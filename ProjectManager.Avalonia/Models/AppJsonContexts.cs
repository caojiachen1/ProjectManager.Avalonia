using System.Text.Json.Serialization;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Models;

/// <summary>
/// AOT-compatible JSON source generator for project persistence (camelCase naming).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(List<PersistedProject>))]
[JsonSerializable(typeof(PersistedProject))]
[JsonSerializable(typeof(ComfyUISettings))]
[JsonSerializable(typeof(NodeJSSettings))]
[JsonSerializable(typeof(DotNetSettings))]
[JsonSerializable(typeof(ProjectStatus))]
internal partial class ProjectJsonContext : JsonSerializerContext
{
}

/// <summary>
/// AOT-compatible JSON source generator for app settings (PascalCase naming).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(AppThemeMode))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
