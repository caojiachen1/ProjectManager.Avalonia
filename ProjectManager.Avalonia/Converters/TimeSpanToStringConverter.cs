using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ProjectManager.Avalonia.Converters;

/// <summary>
/// Converts a TimeSpan (or nullable TimeSpan) to a short hh:mm:ss string, or returns a fallback for null.
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public string NullFallback { get; set; } = "--";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return NullFallback;

        if (value is TimeSpan ts)
        {
            try
            {
                // Format as hours:minutes:seconds, ignoring fractional seconds
                return string.Format(culture, "{0:00}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            }
            catch
            {
                return ts.ToString();
            }
        }

        // If unexpected type, attempt ToString
        return value.ToString() ?? NullFallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
