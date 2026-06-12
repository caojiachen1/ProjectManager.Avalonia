using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ProjectManager.Avalonia.Converters;

/// <summary>
/// 范围验证转换器，用于确保数值在指定范围内
/// 参数格式："min:max" 或 "min:" 或 ":max"
/// 例如："0:" 表示大于等于0，":100" 表示小于等于100，"1:65535" 表示在1-65535范围内
/// </summary>
public class RangeValidationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 转换时不做限制，只是传递值
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
        if (value == null || (value is double d && (double.IsNaN(d) || double.IsInfinity(d))))
            return null;

        if (value is double doubleValue)
        {
            // 解析范围参数
            double? minValue = null;
            double? maxValue = null;

            if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
            {
                var parts = paramStr.Split(':');
                if (parts.Length == 2)
                {
                    if (!string.IsNullOrEmpty(parts[0]) && double.TryParse(parts[0], out double min))
                        minValue = min;
                    if (!string.IsNullOrEmpty(parts[1]) && double.TryParse(parts[1], out double max))
                        maxValue = max;
                }
            }

            // 应用范围限制
            if (minValue.HasValue && doubleValue < minValue.Value)
                doubleValue = minValue.Value;
            if (maxValue.HasValue && doubleValue > maxValue.Value)
                doubleValue = maxValue.Value;

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
