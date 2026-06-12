using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Helpers;

/// <summary>
/// Converts AppThemeMode enum to boolean for RadioButton bindings.
/// Parameter should be "Light", "Dark", or "System" (matching AppThemeMode.ToString()).
/// </summary>
public class AppThemeModeToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AppThemeMode mode || parameter is not string paramName)
            return false;

        if (!Enum.TryParse<AppThemeMode>(paramName, ignoreCase: true, out var targetMode))
            return false;

        return mode == targetMode;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || !boolValue || parameter is not string paramName)
            return AppThemeMode.System;

        if (Enum.TryParse<AppThemeMode>(paramName, ignoreCase: true, out var targetMode))
            return targetMode;

        return AppThemeMode.System;
    }
}
