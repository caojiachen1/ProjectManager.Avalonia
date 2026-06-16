using Avalonia;
using Avalonia.X11;
using System;
using System.Runtime.InteropServices;

namespace ProjectManager.Avalonia.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

            // On Linux, configure X11 rendering for better compatibility.
            // In VMs or environments with limited GPU support, hardware-accelerated
            // rendering can cause popups/dropdowns to render invisibly.
            // Set AVALONIA_SOFTWARE_RENDERING=1 to force software rendering.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var forceSoftware = Environment.GetEnvironmentVariable("AVALONIA_SOFTWARE_RENDERING");
                if (forceSoftware == "1" || forceSoftware == "true")
                {
                    builder = builder.With(new X11PlatformOptions
                    {
                        RenderingMode = new[] { X11RenderingMode.Software }
                    });
                }
                else
                {
                    // Default: try Glx (GPU) first, fall back to software
                    builder = builder.With(new X11PlatformOptions
                    {
                        RenderingMode = new[] { X11RenderingMode.Glx, X11RenderingMode.Software }
                    });
                }
            }

            return builder;
        }
    }
}
