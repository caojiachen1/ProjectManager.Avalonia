using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Microsoft.Win32;

#pragma warning disable CA1416 // Platform compatibility — all Windows-only calls are guarded by RuntimeInformation.IsOSPlatform
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Cross-platform environment variable service.
/// Windows: Registry + EnvironmentVariableTarget + WM_SETTINGCHANGE broadcast.
/// Linux/macOS: ~/.profile, ~/.bashrc, /etc/environment (with limited system-level support).
/// </summary>
public class EnvironmentVariableService
{
    // ==================== Set User Variable ====================

    /// <summary>
    /// Set a user-scope environment variable (no admin required).
    /// </summary>
    public bool SetUserVariable(string name, string value)
    {
        try
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set user environment variable: {ex.Message}");
            return false;
        }
    }

    // ==================== Set System Variable ====================

    /// <summary>
    /// Set a system-scope environment variable (requires admin/root).
    /// </summary>
    public bool SetSystemVariable(string name, string value)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return SetSystemVariableWindows(name, value);
            }
            else
            {
                // On Unix, system-wide env vars are typically in /etc/environment
                // This requires root; we just attempt it directly
                Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Machine);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set system environment variable: {ex.Message}");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool SetSystemVariableWindows(string name, string value)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", true);
        if (key != null)
        {
            key.SetValue(name, value, RegistryValueKind.String);
            BroadcastEnvironmentChange();
            return true;
        }
        return false;
    }

    // ==================== Set System Variable with Elevation ====================

    /// <summary>
    /// Set a system variable, requesting elevation if needed (UAC on Windows, pkexec on Linux, osascript on macOS).
    /// </summary>
    public bool SetSystemVariableWithElevation(string name, string value)
    {
        try
        {
            if (UacHelper.HasAdminPrivileges())
            {
                return SetSystemVariable(name, value);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use 'reg' command with UAC elevation
                var startInfo = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $"add \"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment\" /v \"{name}\" /d \"{value}\" /f",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                if (process?.ExitCode == 0)
                {
                    BroadcastEnvironmentChange();
                    return true;
                }
                return false;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Use pkexec to write to /etc/environment
                var escapedValue = value.Replace("\"", "\\\"");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"bash -c \"grep -q '^ {name}=' /etc/environment && sed -i 's|^{name}=.*|{name}={escapedValue}|' /etc/environment || echo '{name}={escapedValue}' >> /etc/environment\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: use launchctl + plist (limited support)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e 'do shell script \"launchctl setenv {name} {value}\" with administrator privileges'",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set system variable with elevation: {ex.Message}");
            return false;
        }
    }

    // ==================== Delete Variable ====================

    /// <summary>
    /// Delete an environment variable.
    /// </summary>
    public bool DeleteVariable(string name, bool isSystemVariable)
    {
        try
        {
            if (isSystemVariable)
            {
                return DeleteSystemVariableWithElevation(name);
            }
            else
            {
                Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete environment variable: {ex.Message}");
            return false;
        }
    }

    private bool DeleteSystemVariableWithElevation(string name)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $"delete \"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment\" /v \"{name}\" /f",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                if (process?.ExitCode == 0)
                {
                    BroadcastEnvironmentChange();
                    return true;
                }
                return false;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"sed -i '/^{name}=/d' /etc/environment",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            else
            {
                // macOS fallback
                Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Machine);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete system variable with elevation: {ex.Message}");
            return false;
        }
    }

    // ==================== Get Variables ====================

    /// <summary>
    /// Get all user-scope environment variables.
    /// </summary>
    public Dictionary<string, string> GetUserVariables()
    {
        var variables = new Dictionary<string, string>();
        try
        {
            var envVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                string key = entry.Key.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    variables[key] = entry.Value?.ToString() ?? "";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get user environment variables: {ex.Message}");
        }
        return variables;
    }

    /// <summary>
    /// Get all system-scope environment variables.
    /// </summary>
    public Dictionary<string, string> GetSystemVariables()
    {
        var variables = new Dictionary<string, string>();
        try
        {
            var envVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                string key = entry.Key.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    variables[key] = entry.Value?.ToString() ?? "";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get system environment variables: {ex.Message}");
        }
        return variables;
    }

    // ==================== Admin Check ====================

    /// <summary>
    /// Check if the current process has administrator / root privileges.
    /// </summary>
    public bool HasAdminPrivileges() => UacHelper.HasAdminPrivileges();

    // ==================== Batch Operations ====================

    /// <summary>
    /// Batch-set multiple environment variables (for import scenarios).
    /// </summary>
    public async Task<bool> BatchSetVariablesAsync(List<SystemEnvironmentVariable> variables, bool isSystemVariables)
    {
        if (isSystemVariables && !HasAdminPrivileges())
        {
            return await Task.Run(() =>
            {
                foreach (var variable in variables)
                {
                    if (!SetSystemVariableWithElevation(variable.Name, variable.Value))
                    {
                        return false;
                    }
                }
                return true;
            });
        }
        else
        {
            return await Task.Run(() =>
            {
                var target = isSystemVariables ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
                foreach (var variable in variables)
                {
                    try
                    {
                        Environment.SetEnvironmentVariable(variable.Name, variable.Value, target);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to set environment variable {variable.Name}: {ex.Message}");
                        return false;
                    }
                }
                return true;
            });
        }
    }

    // ==================== Windows-Specific Broadcast ====================

    /// <summary>
    /// Broadcast WM_SETTINGCHANGE on Windows to notify other apps of environment changes.
    /// No-op on non-Windows platforms.
    /// </summary>
    private void BroadcastEnvironmentChange()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            IntPtr hwndBroadcast = new IntPtr(0xFFFF); // HWND_BROADCAST
            const int WM_SETTINGCHANGE = 0x001A;
            const int SMTO_ABORTIFHUNG = 0x0002;

            SendMessageTimeout(hwndBroadcast, WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
                SMTO_ABORTIFHUNG, 5000, out IntPtr _);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to broadcast environment change: {ex.Message}");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
}
