using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ProjectManager.Avalonia.Converters;

/// <summary>
/// 转换器用于处理nullable的数字类型与NumberBox的绑定
/// </summary>
public class NullableToDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 将nullable类型转换为double，null值转换为NaN
        if (value == null)
            return double.NaN;

        if (value is float floatValue)
            return (double)floatValue;

        if (value is int intValue)
            return (double)intValue;

        if (value is double doubleValue)
            return doubleValue;

        // 尝试解析字符串
        if (value is string stringValue && double.TryParse(stringValue, out double parsedValue))
            return parsedValue;

        return double.NaN;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 将double转换回nullable类型
        if (value == null || (value is double d && (double.IsNaN(d) || double.IsInfinity(d))))
            return null;

        if (value is double doubleValue)
        {
            // 检查参数是否要求非负值
            if (parameter is string paramStr && paramStr.Contains("NonNegative") && doubleValue < 0)
                return null; // 拒绝负值

            // 根据目标类型转换
            if (targetType == typeof(float?) || targetType == typeof(float))
                return (float)doubleValue;

            if (targetType == typeof(int?) || targetType == typeof(int))
                return (int)Math.Round(doubleValue);

            return doubleValue;
        }

        return null;
    }
}

/// <summary>
/// 专门用于可选数值设置的转换器，null值显示为空，从0开始调整
/// </summary>
public class OptionalNumberConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // null值返回null，让NumberBox显示为空白而不是NaN
        if (value == null)
            return null;

        if (value is float floatValue)
            return (double)floatValue;

        if (value is int intValue)
            return (double)intValue;

        if (value is double doubleValue)
            return doubleValue;

        // 尝试解析字符串
        if (value is string stringValue && double.TryParse(stringValue, out double parsedValue))
            return parsedValue;

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 空值或NaN都转换为null
        if (value == null || (value is double d && (double.IsNaN(d) || double.IsInfinity(d))))
            return null;

        if (value is double doubleValue)
        {
            // 根据目标类型转换
            if (targetType == typeof(float?) || targetType == typeof(float))
                return (float)doubleValue;

            if (targetType == typeof(int?) || targetType == typeof(int))
                return (int)Math.Round(doubleValue);

            if (targetType == typeof(double?) || targetType == typeof(double))
                return doubleValue;
        }

        return null;
    }
}
