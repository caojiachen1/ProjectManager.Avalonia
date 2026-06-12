namespace ProjectManager.Avalonia.Services;

public interface IErrorDisplayService
{
    Task ShowErrorAsync(string message, string title = "错误");
    Task ShowExceptionAsync(Exception exception, string title = "错误");
    Task ShowWarningAsync(string message, string title = "警告");
    Task ShowInfoAsync(string message, string title = "信息");
    Task<bool> ShowConfirmationAsync(string message, string title = "确认");
}
