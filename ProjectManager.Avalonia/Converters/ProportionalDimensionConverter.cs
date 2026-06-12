using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ProjectManager.Avalonia.Converters;

/// <summary>
/// Converts a parent dimension (like height or width) to a proportional value.
/// The conversion factor is passed via the ConverterParameter.
/// </summary>
public class ProportionalDimensionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double parentDimension)
            return AvaloniaProperty.UnsetValue;

        const double Epsilon = 0.5; // 与父尺寸完全相等时减去的像素偏移（避免布局"静止"）。

        return Math.Max(0, parentDimension - Epsilon);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
