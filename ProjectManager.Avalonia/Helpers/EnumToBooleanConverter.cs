using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace ProjectManager.Avalonia.Helpers;

/// <summary>
/// Converts Avalonia ThemeVariant to boolean for RadioButton/ToggleButton bindings.
/// Parameter should be "Dark" or "Light".
/// </summary>
internal class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string themeString)
        {
            throw new ArgumentException("Parameter must be a theme name string (e.g. 'Dark' or 'Light')");
        }

        // Support both ThemeVariant objects and string values
        if (value is ThemeVariant themeVariant)
        {
            var targetVariant = themeString.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
            return themeVariant == targetVariant;
        }

        // Fallback: check application's actual theme variant
        if (Application.Current is { } app)
        {
            var isDark = app.ActualThemeVariant == ThemeVariant.Dark;
            return themeString.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? isDark : !isDark;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string themeString)
        {
            throw new ArgumentException("Parameter must be a theme name string (e.g. 'Dark' or 'Light')");
        }

        if (value is bool boolValue && boolValue)
        {
            return themeString.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        // Return current theme if unchecked
        return Application.Current?.ActualThemeVariant ?? ThemeVariant.Light;
    }
}
