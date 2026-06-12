using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ProjectManager.Avalonia.Helpers;

/// <summary>
/// Cross-platform UAC / privilege elevation helper.
/// Windows: ProcessStartInfo.Verb = "runas"
/// Linux: pkexec / sudo
/// macOS: osascript with administrator privileges
/// </summary>
public static class UacHelper
{
    /// <summary>
    /// Check if the current process is running with administrator / root privileges.
    /// </summary>
    public static bool IsRunAsAdmin()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            else
            {
                // Unix-like: check if running as root (UID 0)
                return geteuid() == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restart the current application with elevated privileges.
    /// </summary>
    public static bool RestartAsAdmin(string arguments = "")
    {
        try
        {
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe))
                return false;

            return RunAsAdmin(currentExe, arguments);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UAC elevation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Run a specific program with elevated / root privileges.
    /// </summary>
    public static bool RunAsAdmin(string fileName, string arguments = "")
    {
        try
        {
            ProcessStartInfo startInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    Verb = "runas", // Triggers UAC prompt
                    UseShellExecute = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try pkexec first (polkit), then fall back to sudo-in-terminal
                startInfo = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"\"{fileName}\" {arguments}",
                    UseShellExecute = false
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: use osascript to request admin privileges
                var escapedArgs = arguments.Replace("\"", "\\\"");
                startInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e 'do shell script \"\\\"{fileName}\\\" {escapedArgs}\" with administrator privileges'",
                    UseShellExecute = false
                };
            }
            else
            {
                Debug.WriteLine("UAC elevation not supported on this platform.");
                return false;
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UAC elevation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if the current process has admin privileges (cross-platform alias for IsRunAsAdmin).
    /// </summary>
    public static bool HasAdminPrivileges() => IsRunAsAdmin();

    // Unix geteuid P/Invoke for checking root status
    [DllImport("libc")]
    private static extern uint geteuid();
}
