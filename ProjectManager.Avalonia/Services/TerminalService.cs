using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.Models;

namespace ProjectManager.Avalonia.Services;

/// <summary>
/// Cross-platform terminal service — manages process lifecycle for running projects.
/// Supports PowerShell/cmd (Windows), bash/sh/zsh (Linux/macOS), and Git Bash.
/// </summary>
public class TerminalService
{
    private readonly Dictionary<string, TerminalSession> _sessions = new();
    private readonly object _lockObject = new();
    private readonly ISettingsService _settingsService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;

    public TerminalService(ISettingsService settingsService, IErrorDisplayService errorDisplayService, ILanguageService languageService)
    {
        _settingsService = settingsService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        _languageService.LanguageChanged += (s, lang) =>
        {
            lock (_lockObject)
            {
                foreach (var session in _sessions.Values)
                {
                    session.RefreshStatus();
                }
            }
        };
    }

    private static Process? TryGetChildProcess(Process process)
    {
        try
        {
            return ProcessInterop.TryResolveRealProcess(process);
        }
        catch
        {
            return null;
        }
    }

    // ==================== Session Management ====================

    public TerminalSession CreateSession(string projectName, string projectPath, string command, Dictionary<string, string>? environmentVariables = null)
    {
        lock (_lockObject)
        {
            var session = new TerminalSession
            {
                ProjectName = projectName,
                ProjectPath = projectPath,
                Command = command,
                StartTime = DateTime.Now,
                EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>()
            };

            _sessions[session.SessionId] = session;
            return session;
        }
    }

    public TerminalSession? GetSession(string sessionId)
    {
        lock (_lockObject)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }

    public IReadOnlyList<TerminalSession> GetAllSessions()
    {
        lock (_lockObject)
        {
            return _sessions.Values.ToList().AsReadOnly();
        }
    }

    public void RemoveSession(string sessionId)
    {
        lock (_lockObject)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                StopSession(session);
                _sessions.Remove(sessionId);
            }
        }
    }

    public void Cleanup()
    {
        lock (_lockObject)
        {
            foreach (var session in _sessions.Values)
            {
                StopSession(session);
            }
            _sessions.Clear();
        }
    }

    // ==================== Start Session ====================

    public async Task<bool> StartSessionAsync(TerminalSession session, Dictionary<string, string>? environmentVariables = null)
    {
        try
        {
            if (session.IsRunning)
            {
                var s0 = await _settingsService.GetSettingsAsync();
                session.AddOutputRawWithTimestamp(
                    _languageService.GetString("Terminal_AlreadyRunning") + "\r\n", s0.ShowTerminalTimestamps);
                return false;
            }

            session.UpdateStatus(TerminalStatus.Starting, false);
            var settings = await _settingsService.GetSettingsAsync();
            session.AddOutputRawWithTimestamp(
                $"{_languageService.GetString("Terminal_StartCommand")}{session.Command}\r\n", settings.ShowTerminalTimestamps);
            session.AddOutputRawWithTimestamp(
                $"{_languageService.GetString("Terminal_WorkingDir")} {session.ProjectPath}\r\n", settings.ShowTerminalTimestamps);

            // Build environment variables (copy to avoid external mutation)
            var envVars = (environmentVariables ?? session.EnvironmentVariables) != null
                ? new Dictionary<string, string>(environmentVariables ?? session.EnvironmentVariables)
                : new Dictionary<string, string>();

            // Build command sequence
            var commandSequence = new List<string>();
            if (envVars != null && envVars.Any())
            {
                session.AddOutputRawWithTimestamp(
                    _languageService.GetString("Terminal_SettingEnvVars") + "\r\n", settings.ShowTerminalTimestamps);
                foreach (var env in envVars)
                {
                    session.AddOutputRawWithTimestamp($"  {env.Key}={env.Value}\r\n", settings.ShowTerminalTimestamps);
                }
                commandSequence.AddRange(BuildEnvCommands(settings.PreferredTerminal, envVars));
            }

            if (!string.IsNullOrWhiteSpace(session.Command))
            {
                commandSequence.Add(session.Command);
            }

            var (fileName, arguments) = GetTerminalCommandWithSequence(settings.PreferredTerminal, commandSequence, settings);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = session.ProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Inject env vars at process level as well
            if (envVars != null && envVars.Any())
            {
                foreach (var kv in envVars)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key))
                        {
                            processStartInfo.Environment[kv.Key] = kv.Value ?? string.Empty;
                        }
                    }
                    catch { /* ignore single variable injection errors */ }
                }
            }

            var process = new Process { StartInfo = processStartInfo };
            session.Process = process;

            var cts = new CancellationTokenSource();
            async Task ReadStreamAsync(Stream stream)
            {
                var buffer = new byte[4096];
                while (!cts.IsCancellationRequested)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch { break; }
                    if (bytesRead <= 0) break;
                    var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    if (!string.IsNullOrEmpty(text))
                    {
                        session.AddOutputRawWithTimestamp(text, settings.ShowTerminalTimestamps);
                    }
                }
            }

            process.Exited += (sender, e) =>
            {
                session.UpdateStatus(TerminalStatus.Stopped, false);
                session.AddOutputRawWithTimestamp(
                    $"{_languageService.GetString("Terminal_ProcessExited")}{process.ExitCode}\r\n", settings.ShowTerminalTimestamps);
                try { cts.Cancel(); } catch { }
            };

            process.EnableRaisingEvents = true;
            process.Start();

            // Detect child processes (e.g., shell launching the real app)
            try
            {
                const int maxAttempts = 10;
                const int delayMs = 150;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    await Task.Delay(delayMs);
                    var child = TryGetChildProcess(process);
                    if (child != null && child.Id != process.Id)
                    {
                        try { child.EnableRaisingEvents = true; } catch { }
                        session.Process = child;
                        session.AddOutputRawWithTimestamp(
                            string.Format(_languageService.GetString("Terminal_ChildProcessDetected"), child.Id, child.ProcessName) + "\r\n",
                            settings.ShowTerminalTimestamps);
                        break;
                    }
                }
            }
            catch { }

            _ = ReadStreamAsync(process.StandardOutput.BaseStream);
            _ = ReadStreamAsync(process.StandardError.BaseStream);

            session.UpdateStatus(TerminalStatus.Running, true);
            session.AddOutputRawWithTimestamp(
                _languageService.GetString("Terminal_Started") + "\r\n", settings.ShowTerminalTimestamps);

            return true;
        }
        catch (Exception ex)
        {
            session.UpdateStatus(TerminalStatus.StartFailed, false);
            var s1 = await _settingsService.GetSettingsAsync();
            session.AddOutputRawWithTimestamp(
                string.Format(_languageService.GetString("Terminal_StartFailedMessage"), ex.Message) + "\r\n", s1.ShowTerminalTimestamps);
            _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(
                string.Format(_languageService.GetString("Terminal_StartFailedMessage"), ex.Message),
                _languageService.GetString("Terminal_StartError")));
            return false;
        }
    }

    // ==================== Stop Session ====================

    public void StopSession(TerminalSession session)
    {
        try
        {
            if (session.Process != null && !session.Process.HasExited)
            {
                try
                {
                    session.Process.Kill(entireProcessTree: true);
                }
                catch { }

                try
                {
                    if (!session.Process.WaitForExit(50))
                    {
                        ForceKillProcessTree(session.Process);
                    }
                }
                catch { }

                var s2 = _settingsService.GetSettingsAsync().Result;
                session.AddOutputRawWithTimestamp(
                    _languageService.GetString("Terminal_ForceStopped") + "\r\n", s2.ShowTerminalTimestamps);
            }
            session.UpdateStatus(TerminalStatus.Stopped, false);
        }
        catch (Exception ex)
        {
            var s3 = _settingsService.GetSettingsAsync().Result;
            session.AddOutputRawWithTimestamp(
                string.Format(_languageService.GetString("Terminal_StopFailedMessage"), ex.Message) + "\r\n", s3.ShowTerminalTimestamps);
            _ = Task.Run(async () => await _errorDisplayService.ShowErrorAsync(
                string.Format(_languageService.GetString("Terminal_StopFailedMessage"), ex.Message),
                _languageService.GetString("Terminal_StopError")));
        }
    }

    /// <summary>
    /// Cross-platform force-kill of a process tree.
    /// Windows: taskkill /PID /F /T
    /// Linux/macOS: pkill -P PID then kill -9 PID
    /// </summary>
    private static void ForceKillProcessTree(Process process)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /F /T",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                proc.Start();
                proc.WaitForExit(2000);
            }
            else
            {
                // Unix: kill child processes first, then the parent
                var childPids = ProcessInterop.EnumerateChildProcessIds(process.Id);
                foreach (var childPid in childPids)
                {
                    try
                    {
                        var killProc = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "kill",
                                Arguments = $"-9 {childPid}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            }
                        };
                        killProc.Start();
                        killProc.WaitForExit(1000);
                    }
                    catch { }
                }

                // Kill the parent
                try
                {
                    var killParent = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "kill",
                            Arguments = $"-9 {process.Id}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };
                    killParent.Start();
                    killParent.WaitForExit(1000);
                }
                catch { }
            }
        }
        catch { }
    }

    // ==================== Terminal Command Builders ====================

    private (string fileName, string arguments) GetTerminalCommandWithSequence(
        string terminalType, List<string> commandSequence, AppSettings settings)
    {
        if (commandSequence == null || !commandSequence.Any())
        {
            return GetDefaultIdleCommand();
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        return terminalType?.ToLower() switch
        {
            "powershell" or "powershell 7" when isWindows =>
                ("powershell.exe", $"-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {string.Join("; ", commandSequence)}\""),

            "cmd" or "command prompt" when isWindows =>
                ("cmd.exe", $"/c {(settings.UseCmdChcp65001 ? "chcp 65001 && " : "")}{string.Join(" && ", commandSequence)}"),

            "git bash" when isWindows =>
                ("bash.exe", $"-c \"export LANG=en_US.UTF-8; export LC_ALL=en_US.UTF-8; export TERM=xterm-256color; {string.Join(" && ", commandSequence)}\""),

            _ => // Cross-platform default
                GetDefaultTerminalCommand(commandSequence, isWindows)
        };
    }

    private static (string fileName, string arguments) GetDefaultTerminalCommand(List<string> commandSequence, bool isWindows)
    {
        if (isWindows)
        {
            // Default to PowerShell on Windows
            return ("powershell.exe",
                $"-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {string.Join("; ", commandSequence)}\"");
        }
        else
        {
            // On Linux/macOS, use /bin/bash or /bin/sh
            var shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
            var joinedCommands = string.Join(" && ", commandSequence);
            return (shell, $"-c \"{joinedCommands}\"");
        }
    }

    private static (string fileName, string arguments) GetDefaultIdleCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd.exe", "/c echo No commands to execute");
        }
        else
        {
            var shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
            return (shell, "-c \"echo No commands to execute\"");
        }
    }

    /// <summary>
    /// Convert environment variables to shell-specific set commands.
    /// </summary>
    private static IEnumerable<string> BuildEnvCommands(string terminalType, Dictionary<string, string> env)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var type = terminalType?.ToLower() ?? "powershell";

        switch (type)
        {
            case "cmd":
            case "command prompt":
                if (!isWindows) yield break;
                foreach (var kv in env)
                {
                    var key = EscapeCmd(kv.Key);
                    var val = EscapeCmd(kv.Value ?? string.Empty);
                    yield return $"set \"{key}={val}\"";
                }
                break;

            case "git bash":
                foreach (var kv in env)
                {
                    var key = EscapeBash(kv.Key);
                    var val = EscapeBash(kv.Value ?? string.Empty);
                    yield return $"export {key}=\"{val}\"";
                }
                break;

            case "powershell":
            case "powershell 7":
                if (!isWindows)
                {
                    // pwsh on Linux/macOS
                    goto default;
                }
                foreach (var kv in env)
                {
                    var key = EscapePwsh(kv.Key);
                    var val = EscapePwsh(kv.Value ?? string.Empty);
                    yield return $"$env:{key} = '{val}'";
                }
                break;

            default:
                // Cross-platform default: export (works in bash, sh, zsh)
                foreach (var kv in env)
                {
                    var key = EscapeBash(kv.Key);
                    var val = EscapeBash(kv.Value ?? string.Empty);
                    yield return $"export {key}=\"{val}\"";
                }
                break;
        }

        static string EscapeCmd(string s) => s.Replace("\"", "\\\"");
        static string EscapeBash(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string EscapePwsh(string s) => s.Replace("'", "''");
    }

    // ==================== Encoding Helpers ====================

    private string FixEncodingIssues(string input)
    {
        try
        {
            if (input.Contains("\uFFFD") || HasGarbledCharacters(input))
            {
                var gbkEncoding = Encoding.GetEncoding("GBK");
                var bytes = Encoding.Default.GetBytes(input);
                return gbkEncoding.GetString(bytes);
            }
            return input;
        }
        catch
        {
            return input;
        }
    }

    private bool HasGarbledCharacters(string input)
    {
        return input.Contains("\uFFFD\uFFFD") ||
               input.Contains("\u9518\uFFFD") ||
               System.Text.RegularExpressions.Regex.IsMatch(input,
                   @"[^\x00-\x7F\u4e00-\u9fff\u3400-\u4dbf\uf900-\ufaff\u3040-\u309f\u30a0-\u30ff]");
    }
}
