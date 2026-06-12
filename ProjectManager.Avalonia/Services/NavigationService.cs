namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Navigation service — routes page navigation requests to the MainWindow.
/// </summary>
public class NavigationService : INavigationService
{
    public event EventHandler<Type>? NavigationRequested;

    public void Navigate(Type pageType) => Navigate(pageType, null);

    public void Navigate(Type pageType, object? parameter)
    {
        NavigationRequested?.Invoke(this, pageType);
    }
}
