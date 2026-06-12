using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Converters;

public class GitStatusToBadgeConverter : IValueConverter
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#2D882D"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#C42B1C"));
    private static readonly IBrush CautionBrush = new SolidColorBrush(Color.Parse("#D48908"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#005FB8"));
    private static readonly IBrush SecondaryBrush = new SolidColorBrush(Color.Parse("#707070"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GitStatus status)
        {
            return status switch
            {
                GitStatus.Clean => SuccessBrush,
                GitStatus.Modified => CautionBrush,
                GitStatus.Staged => InfoBrush,
                GitStatus.Untracked => InfoBrush,
                GitStatus.Conflicted => DangerBrush,
                _ => SecondaryBrush
            };
        }
        return SecondaryBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class HasGitRepositoryToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Project project)
        {
            var hasGitRepo = (project.GitInfo?.IsGitRepository == true) ||
                            (project.GitRepositories?.Count > 0);
            return hasGitRepo;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
