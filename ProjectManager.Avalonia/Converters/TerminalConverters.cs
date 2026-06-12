using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ProjectManager.Avalonia.Converters;

public class BooleanToColorConverter : IValueConverter
{
    private static readonly IBrush RunningBrush = new SolidColorBrush(Colors.LimeGreen);
    private static readonly IBrush StoppedBrush = new SolidColorBrush(Colors.Gray);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isRunning && isRunning ? RunningBrush : StoppedBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}
