using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Cross-platform language service.
/// Manages i18n strings via in-memory dictionary + Avalonia application resources.
/// Language resource files: Resources/Languages/Strings.{lang}.axaml.
/// Thread-safe: GetString may be called from any thread; resource dictionary
/// swaps are marshaled to the UI dispatcher.
/// </summary>
public class LanguageService : ILanguageService
{
    private readonly ISettingsService _settingsService;
    private string _currentLanguage = "zh-CN";

    // Thread-safe in-memory cache of resolved strings for the current language.
    private readonly ConcurrentDictionary<string, string> _strings = new();

    // Snapshot of the entire cache at the end of each successful ChangeLanguage,
    // used as a fallback when GetString is called from a non-UI thread and the
    // cache is empty (e.g., right after Clear() during a language swap).
    private IReadOnlyDictionary<string, string> _lastSnapshot = new Dictionary<string, string>();

    // Avalonia resource provider reference for cleanup
    private global::Avalonia.Controls.IResourceProvider? _currentProvider;

    // Guard for ChangeLanguage reentrancy / concurrent invocations.
    private readonly object _swapLock = new();

    public event EventHandler<string>? LanguageChanged;

    public string CurrentLanguage => _currentLanguage;

    public IReadOnlyList<LanguageInfo> SupportedLanguages { get; } = new List<LanguageInfo>
    {
        new LanguageInfo { Code = "zh-CN", DisplayName = "Chinese (Simplified)", NativeName = "简体中文" },
        new LanguageInfo { Code = "en-US", DisplayName = "English (US)", NativeName = "English" }
    };

    public LanguageService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var savedLanguage = settings.Language;

        if (string.IsNullOrEmpty(savedLanguage))
        {
            var systemCulture = CultureInfo.CurrentUICulture.Name;
            savedLanguage = SupportedLanguages.Any(l => l.Code == systemCulture)
                ? systemCulture
                : "zh-CN";
        }

        // Ensure the dictionary swap runs on the UI dispatcher.
        if (Dispatcher.UIThread.CheckAccess())
            ChangeLanguage(savedLanguage);
        else
            await Dispatcher.UIThread.InvokeAsync(() => ChangeLanguage(savedLanguage));
    }

    public void ChangeLanguage(string languageCode)
    {
        if (!SupportedLanguages.Any(l => l.Code == languageCode))
        {
            languageCode = "zh-CN";
        }

        if (_currentLanguage == languageCode && !_strings.IsEmpty)
        {
            return;
        }

        // Serialize concurrent dictionary swaps.
        lock (_swapLock)
        {
            // Clear the cache so subsequent GetString calls resolve from the new dictionary.
            _strings.Clear();

            try
            {
                var app = global::Avalonia.Application.Current;

                // Remove the previous language resource provider (safe iteration).
                if (_currentProvider != null && app != null)
                {
                    try
                    {
                        var merged = app.Resources.MergedDictionaries;
                        for (int i = merged.Count - 1; i >= 0; i--)
                        {
                            if (ReferenceEquals(merged[i], _currentProvider))
                            {
                                merged.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to remove old language dictionary: {ex.Message}");
                    }
                    _currentProvider = null;
                }

                // Load the new language resource dictionary from avares://
                try
                {
                    var resourceUri = new Uri($"avares://ProjectManager.Avalonia/Resources/Languages/Strings.{languageCode}.axaml");
                    var loaded = global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(resourceUri);
                    if (loaded is global::Avalonia.Controls.ResourceDictionary rd)
                    {
                        if (app != null)
                        {
                            app.Resources.MergedDictionaries.Add(rd);
                            _currentProvider = rd;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Language resource file not found for {languageCode}: {ex.Message}");
                }

                _currentLanguage = languageCode;

                // Pre-warm the cache on the UI thread so subsequent non-UI calls
                // hit the in-memory cache instead of Application.Current.Resources.
                PreWarmCache(app);

                // Snapshot the current cache as a fallback for non-UI threads
                // that race with the next Clear().
                _lastSnapshot = new Dictionary<string, string>(_strings);
                // 保存语言设置
                Task.Run(async () =>
                {
                    var settings = await _settingsService.GetSettingsAsync();
                    settings.Language = languageCode;
                    await _settingsService.SaveSettingsAsync(settings);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to change language: {ex.Message}");
            }
        }

        try
        {
            LanguageChanged?.Invoke(this, languageCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LanguageChanged handler failed: {ex.Message}");
        }

        Debug.WriteLine($"Language changed to: {languageCode}");
    }

    /// <summary>
    /// Pre-populates the cache with a set of common keys while we are guaranteed
    /// to be on the UI thread, so later GetString calls from background threads
    /// hit the cache instead of touching Application.Current.Resources.
    /// </summary>
    private void PreWarmCache(global::Avalonia.Application? app)
    {
        if (app == null) return;

        string[] warmKeys =
        {
            "AppTitle", "Search",
            "Nav_Dashboard", "Nav_Projects", "Nav_Terminal", "Nav_Performance", "Nav_Environment", "Nav_Settings",
            "Button_OK", "Button_Cancel", "Button_Save", "Button_Delete", "Button_Edit", "Button_Add",
            "Button_Browse", "Button_Refresh", "Button_Start", "Button_Stop", "Button_Close",
            "Common_Name", "Common_Value", "Common_Status", "Common_Loading",
            "Status_Running", "Status_Stopped", "Status_Error",
        };

        foreach (var key in warmKeys)
        {
            try
            {
                if (app.Resources.TryGetResource(key, null, out var v) && v is string s)
                {
                    _strings.TryAdd(key, s);
                }
            }
            catch { /* ignore individual key failures */ }
        }
    }

    /// <summary>
    /// Thread-safe string lookup. May be called from any thread.
    /// Fast path: in-memory cache. Fallback: snapshot from last successful ChangeLanguage.
    /// UI-thread only path: resolve from Application.Current.Resources.
    /// </summary>
    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        try
        {
            // Fast path: cache hit.
            if (_strings.TryGetValue(key, out var cached))
                return cached;

            // Snapshot fallback: protects callers on non-UI threads that race
            // with a concurrent Clear() from ChangeLanguage.
            if (_lastSnapshot.TryGetValue(key, out var snapshotValue))
            {
                _strings.TryAdd(key, snapshotValue);
                return snapshotValue;
            }

            // Only touch Application.Current.Resources on the UI dispatcher,
            // since ResourceDictionary is dispatcher-affine.
            if (Dispatcher.UIThread.CheckAccess())
            {
                var app = global::Avalonia.Application.Current;
                if (app != null && app.Resources.TryGetResource(key, null, out var resValue) && resValue is string strValue)
                {
                    _strings.TryAdd(key, strValue);
                    return strValue;
                }
            }
            else
            {
                // Schedule a UI-thread resolution so the cache gets populated
                // for subsequent calls; the current call returns the key itself.
                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var app = global::Avalonia.Application.Current;
                        if (app != null && app.Resources.TryGetResource(key, null, out var v) && v is string s)
                        {
                            _strings.TryAdd(key, s);
                        }
                    }
                    catch { /* ignore */ }
                }, DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetString failed for '{key}': {ex.Message}");
        }

        return key;
    }
}
