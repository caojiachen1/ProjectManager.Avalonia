using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectManager.Avalonia.Models
{
    /// <summary>
    /// 终端会话模型
    /// </summary>
    public partial class TerminalSession : ObservableObject
    {
        [ObservableProperty]
        private string _sessionId = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _projectPath = string.Empty;

        [ObservableProperty]
        private string _command = string.Empty;

        [ObservableProperty]
        private bool _isRunning = false;

        [ObservableProperty]
        private bool _isSelected = false;

        [ObservableProperty]
        private DateTime _startTime = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<string> _outputLines = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusDisplay))]
        private TerminalStatus _status = TerminalStatus.Stopped;

        // StatusDisplay uses hardcoded strings for now (Phase 8 will add i18n)
        [JsonIgnore]
        public string StatusDisplay => Status switch
        {
            TerminalStatus.Running => "Running",
            TerminalStatus.Stopped => "Stopped",
            TerminalStatus.Starting => "Starting",
            TerminalStatus.StartFailed => "Start Failed",
            _ => "Stopped"
        };

        [ObservableProperty]
        private Dictionary<string, string> _environmentVariables = new();

        private Process? _process;
        private readonly object _lockObject = new();
        private bool _nextAtLineStart = true;
        
        // 输出缓冲区，用于批量处理
        private readonly List<string> _outputBuffer = new();
        private DateTime _lastFlushTime = DateTime.MinValue;
        private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(50);
        private System.Threading.Timer? _flushTimer;
        private const int MaxOutputLines = 5000; // 限制最大输出行数

        [JsonIgnore]
        public Process? Process
        {
            get => _process;
            set => _process = value;
        }

        /// <summary>
        /// 添加输出行
        /// </summary>
        /// <param name="line">输出内容</param>
        public void AddOutputLine(string line)
        {
            AddToBuffer($"[{DateTime.Now:HH:mm:ss}] {line}");
        }

        /// <summary>
        /// 追加原始输出片段（不添加时间戳，不强制换行）。
        /// 保留控制字符（如 \r、\b、ESC 序列），用于更真实的终端渲染。
        /// </summary>
        /// <param name="fragment">原始输出片段</param>
        public void AddOutputRaw(string fragment)
        {
            if (fragment == null) return;
            AddToBuffer(fragment);
        }

        /// <summary>
        /// 添加到缓冲区并计划刷新
        /// </summary>
        private void AddToBuffer(string content)
        {
            lock (_lockObject)
            {
                _outputBuffer.Add(content);
                
                // 如果缓冲区较大或距离上次刷新超过间隔，立即刷新
                var now = DateTime.Now;
                if (_outputBuffer.Count >= 20 || (now - _lastFlushTime) >= _flushInterval)
                {
                    FlushBuffer();
                }
                else if (_flushTimer == null)
                {
                    // 启动定时刷新
                    _flushTimer = new System.Threading.Timer(
                        _ => FlushBuffer(),
                        null,
                        _flushInterval,
                        System.Threading.Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>
        /// 刷新缓冲区到UI
        /// </summary>
        private void FlushBuffer()
        {
            List<string>? toAdd = null;
            lock (_lockObject)
            {
                if (_outputBuffer.Count == 0) return;
                
                toAdd = new List<string>(_outputBuffer);
                _outputBuffer.Clear();
                _lastFlushTime = DateTime.Now;
                
                // 停止定时器
                _flushTimer?.Dispose();
                _flushTimer = null;
            }

            if (toAdd != null && toAdd.Count > 0)
            {
                if (!Dispatcher.UIThread.CheckAccess())
                {
                    Dispatcher.UIThread.Post(() => AddOutputLinesToCollection(toAdd));
                }
                else
                {
                    AddOutputLinesToCollection(toAdd);
                }
            }
        }

        /// <summary>
        /// 将输出行添加到集合
        /// </summary>
        private void AddOutputLinesToCollection(List<string> lines)
        {
            foreach (var line in lines)
            {
                OutputLines.Add(line);
            }
            
            // 限制最大行数，防止内存溢出
            while (OutputLines.Count > MaxOutputLines)
            {
                OutputLines.RemoveAt(0);
            }
        }

        /// <summary>
        /// 追加原始输出片段（可选在每行行首添加时间戳）。
        /// 时间戳格式：[HH:mm:ss] ，仅在行首添加，并在遇到 \n 后下一片段再次添加。
        /// </summary>
        public void AddOutputRawWithTimestamp(string fragment, bool enableTimestamps)
        {
            if (fragment == null)
                return;

            if (!enableTimestamps)
            {
                AddOutputRaw(fragment);
                return;
            }

            string stamped;
            lock (_lockObject)
            {
                var nowTag = $"[{DateTime.Now:HH:mm:ss}] ";
                var sb = new System.Text.StringBuilder(fragment.Length + 16);
                int pos = 0;
                while (pos < fragment.Length)
                {
                    if (_nextAtLineStart)
                    {
                        sb.Append(nowTag);
                        _nextAtLineStart = false;
                    }

                    int idx = fragment.IndexOf('\n', pos);
                    if (idx < 0)
                    {
                        sb.Append(fragment, pos, fragment.Length - pos);
                        break;
                    }
                    else
                    {
                        // 包含换行符
                        sb.Append(fragment, pos, (idx - pos + 1));
                        _nextAtLineStart = true;
                        pos = idx + 1;
                    }
                }

                stamped = sb.ToString();
            }
            
            AddToBuffer(stamped);
        }

        /// <summary>
        /// 刷新状态显示（用于语言切换）
        /// </summary>
        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(StatusDisplay));
        }

        /// <summary>
        /// 清空输出
        /// </summary>
        public void ClearOutput()
        {
            lock (_lockObject)
            {
                _outputBuffer.Clear();
                _flushTimer?.Dispose();
                _flushTimer = null;
                _nextAtLineStart = true;
            }
            
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OutputLines.Clear());
            }
            else
            {
                OutputLines.Clear();
            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        /// <param name="status">状态</param>
        /// <param name="isRunning">是否运行中</param>
        public void UpdateStatus(TerminalStatus status, bool isRunning)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Status = status;
                    IsRunning = isRunning;
                });
            }
            else
            {
                Status = status;
                IsRunning = isRunning;
            }
        }
    }

    /// <summary>
    /// 终端状态枚举
    /// </summary>
    public enum TerminalStatus
    {
        Stopped,
        Starting,
        Running,
        StartFailed
    }
}
