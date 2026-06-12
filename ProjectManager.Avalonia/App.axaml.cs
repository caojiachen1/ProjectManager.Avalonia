using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectManager.Avalonia.Services;
using ProjectManager.Avalonia.ViewModels;
using ProjectManager.Avalonia.ViewModels.Pages;
using ProjectManager.Avalonia.ViewModels.Dialogs;
using ProjectManager.Avalonia.Views;
using ProjectManager.Avalonia.Views.Pages;

namespace ProjectManager.Avalonia;

public partial class App : Application
{
    // DI Host — 与原 WPF 项目保持一致的 Generic Host 模式
    private static readonly Lazy<IHost> _hostLazy = new Lazy<IHost>(() => CreateHost(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static IHost _host => _hostLazy.Value;

    /// <summary>
    /// Gets the service provider from the DI container.
    /// </summary>
    public static IServiceProvider Services => _host.Services;

    private static IHost CreateHost() => Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty);
        })
        .ConfigureServices((context, services) =>
        {
            // === Services ===
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<IErrorDisplayService, ErrorDisplayService>();
            services.AddSingleton<IPerformanceMonitorService, PerformanceMonitorService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ILanguageService, LanguageService>();
            services.AddSingleton<IProjectSettingsWindowService, ProjectSettingsWindowService>();
            services.AddSingleton<TerminalService>();
            services.AddSingleton<EnvironmentVariableService>();

            // === Main Window + ViewModel ===
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<MainWindow>();

            // === Page ViewModels ===
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<ProjectsViewModel>();
            services.AddSingleton<AddProjectViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<TerminalViewModel>();
            services.AddSingleton<PerformanceViewModel>();
            services.AddSingleton<SystemEnvironmentVariablesViewModel>();

            // === Dialog ViewModels ===
            services.AddTransient<NewProjectDialogViewModel>();
            services.AddTransient<ProjectEditDialogViewModel>();
            services.AddTransient<GitManagementDialogViewModel>();
            services.AddTransient<GitCloneDialogViewModel>();
            services.AddTransient<EnvironmentVariablesDialogViewModel>();
            services.AddTransient<ComfyUIProjectSettingsViewModel>();
            services.AddTransient<NodeJSProjectSettingsViewModel>();
            services.AddTransient<DotNetProjectSettingsViewModel>();
            services.AddTransient<GenericProjectSettingsViewModel>();
            services.AddTransient<ComfyUIPluginsManagerViewModel>();
            services.AddTransient<EditEnvironmentVariableViewModel>();

            // === Dialog Windows ===
            services.AddTransient<Views.Dialogs.NewProjectWindow>();
            services.AddTransient<Views.Dialogs.ProjectEditWindow>();
            services.AddTransient<Views.Dialogs.GitManagementWindow>();
            services.AddTransient<Views.Dialogs.GitCloneWindow>();
            services.AddTransient<Views.Dialogs.EnvironmentVariablesWindow>();
            services.AddTransient<Views.Dialogs.ComfyUIProjectSettingsWindow>();
            services.AddTransient<Views.Dialogs.NodeJSProjectSettingsWindow>();
            services.AddTransient<Views.Dialogs.DotNetProjectSettingsWindow>();
            services.AddTransient<Views.Dialogs.GenericProjectSettingsWindow>();
            services.AddTransient<Views.Dialogs.ComfyUIPluginsManagerWindow>();
            services.AddTransient<Views.Dialogs.EditEnvironmentVariableWindow>();
        }).Build();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 启动 DI Host
            await _host.StartAsync();

            // 初始化语言服务（必须在显示主窗口前完成，否则首帧会使用 App.axaml 中硬编码的默认语言）
            var languageService = Services.GetService<ILanguageService>() as LanguageService;
            if (languageService != null)
            {
                await languageService.InitializeAsync();
            }

            // 初始化主题服务（应用保存的主题偏好）
            var themeService = Services.GetService<IThemeService>();
            if (themeService != null)
            {
                await themeService.InitializeAsync();
            }

            // 获取 MainWindow 并显示
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // 注册关闭事件
            desktop.ShutdownRequested += OnShutdownRequested;

            // 全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        try
        {
            var terminalService = Services.GetService<TerminalService>();
            terminalService?.Cleanup();
        }
        catch { /* 退出阶段忽略清理异常 */ }

        try
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        catch { }
    }

    private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var errorService = Services.GetService<IErrorDisplayService>();
            if (errorService != null && e.ExceptionObject is Exception ex)
            {
                _ = errorService.ShowExceptionAsync(ex, "未处理系统异常");
            }
        }
        catch { }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var errorService = Services.GetService<IErrorDisplayService>();
            if (errorService != null)
            {
                _ = errorService.ShowExceptionAsync(e.Exception, "未观察任务异常");
            }
        }
        catch { }
        finally
        {
            e.SetObserved();
        }
    }
}
