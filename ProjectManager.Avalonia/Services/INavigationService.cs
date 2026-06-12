using Avalonia;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Manages navigation between pages.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigate to the specified page type.
    /// </summary>
    void Navigate(Type pageType);

    /// <summary>
    /// Navigate to the specified page type with a parameter.
    /// </summary>
    void Navigate(Type pageType, object? parameter);
}
