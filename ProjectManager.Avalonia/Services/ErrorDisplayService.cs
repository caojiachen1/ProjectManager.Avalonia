using FluentAvalonia.UI.Controls;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Error display service using FluentAvalonia FAContentDialog.
/// </summary>
public class ErrorDisplayService : IErrorDisplayService
{
    public async Task ShowErrorAsync(string message, string title = "错误")
    {
        await ShowDialogAsync(message, title);
    }

    public async Task ShowWarningAsync(string message, string title = "警告")
    {
        await ShowDialogAsync(message, title);
    }

    public async Task ShowInfoAsync(string message, string title = "信息")
    {
        await ShowDialogAsync(message, title);
    }

    public async Task<bool> ShowConfirmationAsync(string message, string title = "确认")
    {
        var dialog = new FAContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消"
        };

        var result = await dialog.ShowAsync();
        return result == FAContentDialogResult.Primary;
    }

    public async Task ShowExceptionAsync(Exception exception, string title = "错误")
    {
        var message = exception.ToString();
        await ShowDialogAsync(message, title);
    }

    private static async Task ShowDialogAsync(string content, string title)
    {
        var dialog = new FAContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "关闭"
        };

        await dialog.ShowAsync();
    }
}
