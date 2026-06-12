using System;
using System.Collections.Generic;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Language info metadata.
/// </summary>
public class LanguageInfo
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string NativeName { get; init; } = string.Empty;
}

/// <summary>
/// Manages i18n language resource loading and switching.
/// </summary>
public interface ILanguageService
{
    /// <summary>Current language code (e.g. "zh-CN", "en-US").</summary>
    string CurrentLanguage { get; }

    /// <summary>Supported languages with metadata.</summary>
    IReadOnlyList<LanguageInfo> SupportedLanguages { get; }

    /// <summary>Initialize language service from settings.</summary>
    Task InitializeAsync();

    /// <summary>Switch to the specified language.</summary>
    void ChangeLanguage(string languageCode);

    /// <summary>Get a localized string by resource key. Returns the key itself if not found.</summary>
    string GetString(string key);

    /// <summary>Raised when the active language changes. Payload is the new language code.</summary>
    event EventHandler<string>? LanguageChanged;
}
