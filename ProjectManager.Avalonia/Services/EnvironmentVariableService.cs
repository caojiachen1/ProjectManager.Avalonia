using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
/// Linux/macOS: ~/.profile, /etc/environment.
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
                return true;
            }
            else
            {
                return SetUserVariableUnix(name, value);
            }
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
                    return true;
                }
                else
                {
                    return DeleteUserVariableUnix(name);
                }
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
    /// On Linux/macOS, uses Environment.GetEnvironmentVariables() (Process target) to get
    /// the current session's environment, which includes inherited variables from the
    /// desktop session, shell profiles, and PAM modules.
    /// </summary>
    public Dictionary<string, string> GetUserVariables()
    {
        var variables = new Dictionary<string, string>();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                // On Linux/macOS, EnvironmentVariableTarget.User is a no-op.
                // Use Process target to get all current environment variables,
                // which reflects the actual session environment.
                variables = GetProcessEnvironmentVariables();
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
    /// On Linux/macOS, uses Environment.GetEnvironmentVariables() (Process target).
    /// </summary>
    public Dictionary<string, string> GetSystemVariables()
    {
        var variables = new Dictionary<string, string>();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                // On Linux/macOS, use process environment as the source of truth.
                // This includes variables from /etc/environment, PAM, and desktop session.
                variables = GetProcessEnvironmentVariables();
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
                foreach (var variable in variables)
                {
                    if (!isSystemVariables)
                    {
                        if (!SetUserVariable(variable.Name, variable.Value))
                            return false;
                    }
                    else
                    {
                        if (!SetSystemVariable(variable.Name, variable.Value))
                            return false;
                    }
                }
                return true;
            });
        }
    }

    // ==================== Linux/macOS Variable Reading ====================

    /// <summary>
    /// Get all environment variables from the current process (.NET Process target API).
    /// This is the most reliable way to read env vars on Linux/macOS since
    /// EnvironmentVariableTarget.User/Machine are no-ops on these platforms.
    /// </summary>
    private static Dictionary<string, string> GetProcessEnvironmentVariables()
    {
        var variables = new Dictionary<string, string>();
        var envVars = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            string key = entry.Key.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(key))
            {
                variables[key] = entry.Value?.ToString() ?? "";
            }
        }
        return variables;
    }

    // ==================== Linux/macOS User Variable Implementation ====================

    /// <summary>
    /// Get the path to the user's shell profile for persisting environment variables.
    /// Tries ~/.profile first (most universal), falls back to ~/.bashrc on Linux.
    /// </summary>
    private static string GetUserProfilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";

        // Prefer ~/.profile as it's read by most shells on login
        var profilePath = Path.Combine(home, ".profile");
        return profilePath;
    }

    /// <summary>
    /// Parse export statements from a shell profile file to extract environment variables.
    /// Matches: export NAME="value", export NAME='value', export NAME=value
    /// Also matches: NAME="value" (without export, as used in /etc/environment)
    /// </summary>
    private static Dictionary<string, string> ParseEnvFile(string filePath, bool requireExport = true)
    {
        var variables = new Dictionary<string, string>();
        if (!File.Exists(filePath))
            return variables;

        try
        {
            var lines = File.ReadAllLines(filePath);
            // Pattern: optional 'export', NAME, '=', value (optionally quoted)
            var pattern = requireExport
                ? new Regex(@"^\s*export\s+([A-Za-z_][A-Za-z0-9_]*)=(?:""([^""]*)""|'([^']*)'|(\S*))\s*(?:#.*)?$")
                : new Regex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)=(?:""([^""]*)""|'([^']*)'|(\S*))\s*(?:#.*)?$");

            foreach (var line in lines)
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    // Value is in group 2 (double-quoted), 3 (single-quoted), or 4 (unquoted)
                    var value = match.Groups[2].Success ? match.Groups[2].Value
                              : match.Groups[3].Success ? match.Groups[3].Value
                              : match.Groups[4].Value;
                    variables[name] = value;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse env file {filePath}: {ex.Message}");
        }

        return variables;
    }

    /// <summary>
    /// Read user environment variables from ~/.profile on Linux/macOS.
    /// </summary>
    private Dictionary<string, string> GetUserVariablesUnix()
    {
        var variables = new Dictionary<string, string>();

        // Read from ~/.profile
        var profilePath = GetUserProfilePath();
        var profileVars = ParseEnvFile(profilePath, requireExport: true);
        foreach (var kv in profileVars)
            variables[kv.Key] = kv.Value;

        // Also check ~/.bashrc for bash-specific variables (Linux)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
                home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";

            var bashrcPath = Path.Combine(home, ".bashrc");
            var bashrcVars = ParseEnvFile(bashrcPath, requireExport: true);
            foreach (var kv in bashrcVars)
                variables.TryAdd(kv.Key, kv.Value); // Don't override profile values
        }

        return variables;
    }

    /// <summary>
    /// Read system environment variables from /etc/environment on Linux/macOS.
    /// </summary>
    private Dictionary<string, string> GetSystemVariablesUnix()
    {
        // /etc/environment uses NAME=value format (no 'export' keyword)
        return ParseEnvFile("/etc/environment", requireExport: false);
    }

    /// <summary>
    /// Set a user environment variable by writing an export line to ~/.profile.
    /// Updates existing entry or appends new one.
    /// </summary>
    private bool SetUserVariableUnix(string name, string value)
    {
        try
        {
            var profilePath = GetUserProfilePath();
            var escapedValue = value.Replace("\"", "\\\"");
            var exportLine = $"export {name}=\"{escapedValue}\"";

            if (File.Exists(profilePath))
            {
                var content = File.ReadAllText(profilePath);
                // Try to find and replace existing export line for this variable
                var pattern = new Regex($@"^\s*export\s+{Regex.Escape(name)}=.*$", RegexOptions.Multiline);
                if (pattern.IsMatch(content))
                {
                    content = pattern.Replace(content, exportLine);
                    File.WriteAllText(profilePath, content);
                }
                else
                {
                    // Append new export line
                    File.AppendAllText(profilePath, $"\n{exportLine}\n");
                }
            }
            else
            {
                File.WriteAllText(profilePath, $"# Environment variables managed by ProjectManager\n{exportLine}\n");
            }

            // Also set in current process so child processes see it
            Environment.SetEnvironmentVariable(name, value);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set user variable on Unix: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete a user environment variable by removing the export line from ~/.profile.
    /// </summary>
    private bool DeleteUserVariableUnix(string name)
    {
        try
        {
            var profilePath = GetUserProfilePath();
            if (File.Exists(profilePath))
            {
                var content = File.ReadAllText(profilePath);
                var pattern = new Regex($@"^\s*export\s+{Regex.Escape(name)}=.*\n?", RegexOptions.Multiline);
                content = pattern.Replace(content, "");
                File.WriteAllText(profilePath, content);
            }

            // Also remove from current process
            Environment.SetEnvironmentVariable(name, null);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete user variable on Unix: {ex.Message}");
            return false;
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
