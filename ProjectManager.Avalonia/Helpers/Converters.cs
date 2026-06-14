using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.FluentIcons;
using Avalonia.Media;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Helpers;

public class StatusToBadgeConverter : IValueConverter
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#2D882D"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#C42B1C"));
    private static readonly IBrush CautionBrush = new SolidColorBrush(Color.Parse("#D48908"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#005FB8"));
    private static readonly IBrush SecondaryBrush = new SolidColorBrush(Color.Parse("#707070"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Running => SuccessBrush,
                ProjectStatus.Stopped => SecondaryBrush,
                ProjectStatus.Starting => CautionBrush,
                ProjectStatus.Stopping => CautionBrush,
                ProjectStatus.Error => DangerBrush,
                _ => SecondaryBrush
            };
        }
        return SecondaryBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ProjectStatusToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        object? res = null;

        string GetResource(string key, string fallback)
            => (app?.Resources.TryGetResource(key, null, out res) == true && res is string s) ? s : fallback;

        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Running => GetResource("Status_Running", "Running"),
                ProjectStatus.Stopped => GetResource("Status_Stopped", "Stopped"),
                ProjectStatus.Starting => GetResource("Status_Starting", "Starting"),
                ProjectStatus.Stopping => GetResource("Status_Stopping", "Stopping"),
                ProjectStatus.Error => GetResource("Status_Error", "Error"),
                _ => GetResource("Status_Unknown", "Unknown")
            };
        }
        return GetResource("Status_Unknown", "Unknown");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToStartEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
            return status == ProjectStatus.Stopped || status == ProjectStatus.Error;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToStopEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
            return status == ProjectStatus.Running || status == ProjectStatus.Starting;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue) return !boolValue;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result = value is int count && count > 0;
        if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            result = !result;
        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ResourceStringFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string resourceKey)
        {
            var app = Application.Current;
            if (app != null && app.Resources.TryGetResource(resourceKey, null, out var resValue) && resValue is string format && !string.IsNullOrEmpty(format))
                return string.Format(format, value);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class FrameworkToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string framework && !string.IsNullOrEmpty(framework))
        {
            var app = Application.Current;
            object? res = null;
            return framework switch
            {
                "ComfyUI" => (app?.Resources.TryGetResource("FrameworkDesc_ComfyUI", null, out res) == true && res is string s) ? s : "ComfyUI image generation workflow",
                "Node.js" => (app?.Resources.TryGetResource("FrameworkDesc_NodeJS", null, out res) == true && res is string s) ? s : "Node.js JavaScript runtime",
                ".NET" => (app?.Resources.TryGetResource("FrameworkDesc_DotNet", null, out res) == true && res is string s) ? s : ".NET application",
                "其他" => (app?.Resources.TryGetResource("FrameworkDesc_Other", null, out res) == true && res is string s) ? s : "Custom project type",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BooleanToInverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

public class StatusToToggleButtonTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        object? res = null;

        string GetResource(string key, string fallback)
            => (app?.Resources.TryGetResource(key, null, out res) == true && res is string s) ? s : fallback;

        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Running => GetResource("Button_Stop", "停止"),
                ProjectStatus.Starting => GetResource("Status_Starting", "启动中..."),
                ProjectStatus.Stopping => GetResource("Status_Stopping", "停止中..."),
                ProjectStatus.Stopped => GetResource("Button_Start", "启动"),
                ProjectStatus.Error => GetResource("Button_Start", "启动"),
                _ => GetResource("Button_Start", "启动")
            };
        }
        return GetResource("Button_Start", "启动");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToToggleButtonIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Running => SymbolRegular.Stop24,
                ProjectStatus.Starting => SymbolRegular.Play24,
                ProjectStatus.Stopping => SymbolRegular.Stop24,
                ProjectStatus.Stopped => SymbolRegular.Play24,
                ProjectStatus.Error => SymbolRegular.Play24,
                _ => SymbolRegular.Play24
            };
        }
        return SymbolRegular.Play24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToToggleButtonEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
            return status != ProjectStatus.Starting && status != ProjectStatus.Stopping;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToToggleButtonAppearanceConverter : IValueConverter
{
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#C42B1C"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#005FB8"));
    private static readonly IBrush CautionBrush = new SolidColorBrush(Color.Parse("#D48908"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Running => DangerBrush,
                ProjectStatus.Starting => CautionBrush,
                ProjectStatus.Stopping => CautionBrush,
                ProjectStatus.Stopped => InfoBrush,
                ProjectStatus.Error => InfoBrush,
                _ => InfoBrush
            };
        }
        return InfoBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class FrameworkToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string framework && parameter is string targetFramework)
            return string.Equals(framework, targetFramework, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ComfyUIFrameworkToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string framework && !string.IsNullOrWhiteSpace(framework)
            && framework.Equals("ComfyUI", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ProjectCommandParameterConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is Project project && values[1] is string command)
            return (project, command);
        return AvaloniaProperty.UnsetValue;
    }
}

public class FrameworkToLocalizedNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string framework && !string.IsNullOrEmpty(framework))
        {
            var app = Application.Current;
            object? res = null;
            return framework switch
            {
                "ComfyUI" => (app?.Resources.TryGetResource("Framework_ComfyUI", null, out res) == true && res is string s) ? s : "ComfyUI",
                "Node.js" => (app?.Resources.TryGetResource("Framework_NodeJS", null, out res) == true && res is string s) ? s : "Node.js",
                ".NET" => (app?.Resources.TryGetResource("Framework_DotNet", null, out res) == true && res is string s) ? s : ".NET",
                "其他" => (app?.Resources.TryGetResource("Framework_Other", null, out res) == true && res is string s) ? s : "Other",
                _ => framework
            };
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToTabBackgroundConverter : IValueConverter
{
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#000000"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#2D2D30"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? SelectedBrush : DefaultBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
