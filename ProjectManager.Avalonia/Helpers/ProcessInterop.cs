using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ProjectManager.Avalonia.Helpers;

/// <summary>
/// Cross-platform helper for resolving real (non-shell) child processes
/// and aggregating process-tree memory usage.
/// Windows: CreateToolhelp32Snapshot P/Invoke
/// Linux: /proc filesystem
/// macOS: ps command fallback
/// </summary>
internal static class ProcessInterop
{
    private static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "conhost", "conhost.exe",
        "bash", "bash.exe",
        "sh", "sh.exe",
        "wsl", "wsl.exe",
        "wt", "wt.exe",
        "git-bash", "git-bash.exe",
        "wezterm", "wezterm.exe",
        // Linux/macOS common shells
        "zsh", "fish", "csh", "tcsh", "ksh", "dash"
    };

    public static Process? TryResolveRealProcess(Process? candidate)
    {
        if (candidate == null)
            return null;

        try
        {
            if (!candidate.HasExited && !IsShellProcess(candidate))
            {
                return candidate;
            }
        }
        catch
        {
            return null;
        }

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        if (!TryEnqueue(candidate.Id))
        {
            return null;
        }

        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            foreach (var childPid in EnumerateChildProcessIds(pid))
            {
                if (!TryEnqueue(childPid))
                    continue;

                try
                {
                    var child = Process.GetProcessById(childPid);
                    if (child.HasExited)
                        continue;

                    if (!IsShellProcess(child))
                        return child;

                    queue.Enqueue(child.Id);
                }
                catch
                {
                    // Ignore processes we cannot access
                }
            }
        }

        try
        {
            return candidate.HasExited ? null : candidate;
        }
        catch
        {
            return null;
        }

        bool TryEnqueue(int pid)
        {
            if (pid <= 0)
                return false;
            if (visited.Add(pid))
            {
                queue.Enqueue(pid);
                return true;
            }
            return false;
        }
    }

    public static bool TryGetAggregatedMemoryUsage(Process root, out double workingSetMb, out double privateMemoryMb, bool includeShellDescendants = false)
    {
        workingSetMb = 0;
        privateMemoryMb = 0;

        try
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            void Enqueue(int pid)
            {
                if (pid <= 0)
                    return;
                if (visited.Add(pid))
                {
                    queue.Enqueue(pid);
                }
            }

            Enqueue(root.Id);

            while (queue.Count > 0)
            {
                var pid = queue.Dequeue();
                Process? proc = null;
                try
                {
                    proc = Process.GetProcessById(pid);
                }
                catch
                {
                    continue;
                }

                using (proc)
                {
                    var isShell = IsShellProcess(proc);
                    var shouldInclude = pid == root.Id || !isShell || includeShellDescendants;
                    if (shouldInclude)
                    {
                        try
                        {
                            workingSetMb += BytesToMegabytes(proc.WorkingSet64);
                            privateMemoryMb += BytesToMegabytes(proc.PrivateMemorySize64);
                        }
                        catch
                        {
                            // ignore per-process access errors but continue traversing children
                        }
                    }

                    foreach (var childPid in EnumerateChildProcessIds(proc.Id))
                    {
                        Enqueue(childPid);
                    }
                }
            }

            return workingSetMb > 0 || privateMemoryMb > 0;
        }
        catch
        {
            workingSetMb = 0;
            privateMemoryMb = 0;
            return false;
        }
    }

    private static double BytesToMegabytes(long bytes) => bytes / 1024d / 1024d;

    /// <summary>
    /// Enumerate child process IDs for a given parent PID. Cross-platform.
    /// </summary>
    public static IReadOnlyList<int> EnumerateChildProcessIds(int parentPid)
    {
        if (parentPid <= 0)
            return Array.Empty<int>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return EnumerateChildProcessIdsWindows(parentPid);
        }
        else
        {
            return EnumerateChildProcessIdsUnix(parentPid);
        }
    }

    public static bool IsShellProcess(Process? process)
    {
        if (process == null)
            return false;

        try
        {
            return IsShellProcessName(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsShellProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var nameOnly = Path.GetFileName(processName);
        return ShellProcessNames.Contains(nameOnly ?? processName);
    }

    // ==================== Windows Implementation ====================

    private static IReadOnlyList<int> EnumerateChildProcessIdsWindows(int parentPid)
    {
        var list = new List<int>();

        var snapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, 0u);
        if (snapshot == IntPtr.Zero || snapshot == INVALID_HANDLE_VALUE)
            return list;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    if ((int)entry.th32ParentProcessID == parentPid)
                    {
                        list.Add((int)entry.th32ProcessID);
                    }
                } while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return list;
    }

    private const int MAX_PATH = 260;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [Flags]
    private enum SnapshotFlags : uint
    {
        Process = 0x00000002,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // ==================== Open in File Manager ====================

    /// <summary>
    /// Open a folder in the platform's default file manager.
    /// Windows: explorer.exe, Linux: xdg-open, macOS: open.
    /// </summary>
    public static void OpenInFileManager(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
    }

    /// <summary>
    /// Get the default initial directory for file pickers (e.g., Python executable).
    /// Falls back to UserProfile when ProgramFiles is empty (common on Linux).
    /// </summary>
    public static string GetDefaultFilePickerDirectory()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
            return programFiles;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
            return userProfile;

        return "/";
    }

    // ==================== Unix (Linux + macOS) Implementation ====================

    private static IReadOnlyList<int> EnumerateChildProcessIdsUnix(int parentPid)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return EnumerateChildProcessIdsLinux(parentPid);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return EnumerateChildProcessIdsMacOS(parentPid);
        }

        return Array.Empty<int>();
    }

    /// <summary>
    /// Linux: Read /proc/[pid]/stat to find child processes.
    /// </summary>
    private static IReadOnlyList<int> EnumerateChildProcessIdsLinux(int parentPid)
    {
        var children = new List<int>();
        try
        {
            var procDir = new DirectoryInfo("/proc");
            foreach (var dir in procDir.GetDirectories())
            {
                if (!int.TryParse(dir.Name, out int pid))
                    continue;

                try
                {
                    var statFile = Path.Combine(dir.FullName, "stat");
                    if (!File.Exists(statFile))
                        continue;

                    var statContent = File.ReadAllText(statFile);
                    // Format: pid (comm) state ppid ...
                    // Find the closing parenthesis to skip comm (which may contain spaces/parens)
                    int closeParen = statContent.LastIndexOf(')');
                    if (closeParen < 0) continue;

                    var parts = statContent[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    // parts[0] = state, parts[1] = ppid
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int ppid) && ppid == parentPid)
                    {
                        children.Add(pid);
                    }
                }
                catch
                {
                    // Skip inaccessible processes
                }
            }
        }
        catch
        {
            // /proc not accessible
        }
        return children;
    }

    /// <summary>
    /// macOS: Use 'ps' command to find child processes.
    /// </summary>
    private static IReadOnlyList<int> EnumerateChildProcessIdsMacOS(int parentPid)
    {
        var children = new List<int>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = $"-o pid=,ppid= -ax",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return children;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2
                    && int.TryParse(parts[0], out int pid)
                    && int.TryParse(parts[1], out int ppid)
                    && ppid == parentPid)
                {
                    children.Add(pid);
                }
            }
        }
        catch
        {
            // ps command failed
        }
        return children;
    }
}
