using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ProjectManager.Avalonia.ViewModels;

namespace ProjectManager.Avalonia;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// Supports both "View" and "Page" naming conventions.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var fullName = param.GetType().FullName!;

        // Try "ViewModel" → "Page" (our primary convention)
        var pageName = fullName.Replace("ViewModel", "Page", StringComparison.Ordinal);
        var pageType = Type.GetType(pageName);
        if (pageType != null)
            return (Control)Activator.CreateInstance(pageType)!;

        // Try "ViewModel" → "View" (fallback)
        var viewName = fullName.Replace("ViewModel", "View", StringComparison.Ordinal);
        var viewType = Type.GetType(viewName);
        if (viewType != null)
            return (Control)Activator.CreateInstance(viewType)!;

        // Also try replacing "ViewModels" namespace with "Views"
        var altPageName = fullName
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "Page", StringComparison.Ordinal);
        var altPageType = Type.GetType(altPageName);
        if (altPageType != null)
            return (Control)Activator.CreateInstance(altPageType)!;

        return new TextBlock { Text = "Not Found: " + fullName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
