using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;

namespace AIvisiontest.Core.Logging
{
    /// <summary>
    /// 日志服务实现：同时输出到 Debug 窗口 和 本地日志文件。
    /// 支持异步写入队列、按天分割、超大滚动、高频重复日志抑制、旧日志自动清理。
    /// </summary>
    public class LogService : ILogService, IDisposable
    {
        // ── 配置 ────────────────────────────────────────────────────────────────

        /// <summary>是否同时写入文件（默认开启）</summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>是否同时输出到 Debug 窗口（默认开启）</summary>
        public bool EnableDebugOutput { get; set; } = true;

        /// <summary>最低输出级别（低于此级别的日志会被忽略）</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        /// <summary>单个日志文件的最大大小（字节），超过后在当天内继续滚动，默认 10 MB</summary>
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// 旧日志保留天数（默认 30 天）。
        /// 超过此天数的 .log 文件会在启动时自动删除。
        /// 设为 0 可关闭自动清理。
        /// </summary>
        public int RetainDays { get; set; } = 30;

        /// <summary>
        /// 重复日志抑制窗口（默认 10 秒）。
        /// 同一条消息在此窗口内连续出现超过 DedupThreshold 次后，后续会被合并抑制。
        /// 设为 TimeSpan.Zero 可关闭此功能。
        /// </summary>
        public TimeSpan DedupWindow { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 同一条消息在 DedupWindow 内最多正常写入的次数，超出后开始抑制（默认 3 次）。
        /// </summary>
        public int DedupThreshold { get; set; } = 3;

        // ── UI 事件 ──────────────────────────────────────────────────────────────

        /// <summary>有新日志时触发，ViewModel 订阅此事件实时更新界面</summary>
        public event Action<LogEntry>? OnLogEntry;


        // ── 异步队列 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 异步写入队列容量（默认 4096 条）。
        /// 队列满时丢弃最旧的一条，保证调用方永远不阻塞。
        /// </summary>
        private const int ChannelCapacity = 4096;

        // Channel 只传格式化好的字符串，生产者（业务线程）写，消费者（后台线程）读
        private readonly Channel<string> _channel;
        private readonly Thread _consumerThread;

        // ── 私有字段 ─────────────────────────────────────────────────────────────

        private readonly string _logDirectory;
        private readonly string _logFilePrefix;
        private readonly object _dedupLock = new(); // 仅保护去重状态（不再锁 IO）
        private StreamWriter? _writer;
        private string? _currentLogFilePath;

        /// <summary>当前日志文件所属日期（用于按天切割检测）</summary>
        private DateTime _currentLogDate;

        // 去重状态（只在调用方线程访问，受 _dedupLock 保护）
        private string? _lastDedupKey;
        private int _dedupCount;
        private DateTime _dedupWindowStart;

        // ── 构造函数 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 创建日志服务
        /// </summary>
        /// <param name="logDirectory">日志目录，默认为 exe 同级目录下的 Logs 文件夹</param>
        /// <param name="logFilePrefix">日志文件前缀，默认为 app</param>
        public LogService(string? logDirectory = null, string logFilePrefix = "app")
        {
            _logDirectory = logDirectory
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            _logFilePrefix = logFilePrefix;

            Directory.CreateDirectory(_logDirectory);
            CleanupOldLogs();
            OpenNewLogFile(DateTime.Now, resetSequence: true);

            // ── 初始化异步队列 ────────────────────────────────────────────────
            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ChannelCapacity)
            {
                SingleWriter = false,   // 多个业务线程都可以写
                SingleReader = true,    // 只有一个后台消费线程读
                FullMode = BoundedChannelFullMode.DropOldest  // 满了丢最旧的，不阻塞调用方
            });

            // 后台消费线程：IsBackground=true 保证主进程退出时不会被它卡住
            _consumerThread = new Thread(ConsumeLoop)
            {
                IsBackground = true,
                Name = "LogService.Consumer"
            };
            _consumerThread.Start();

            // 注册进程退出事件，强制 flush（应对任务管理器强杀等场景）
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        // ── 公开方法 ─────────────────────────────────────────────────────────────

        public void Debug(string message, string? tag = null)
            => Log(LogLevel.Debug, message, null, tag);

        public void Info(string message, string? tag = null)
            => Log(LogLevel.Info, message, null, tag);

        public void Warning(string message, string? tag = null)
            => Log(LogLevel.Warning, message, null, tag);

        public void Error(string message, Exception? ex = null, string? tag = null)
            => Log(LogLevel.Error, message, ex, tag);

        public void Fatal(string message, Exception? ex = null, string? tag = null)
            => Log(LogLevel.Fatal, message, ex, tag);

        /// <summary>
        /// 写入日志核心方法。
        /// 调用方线程：去重判断 + 格式化 + 入队（非阻塞）。
        /// 实际 IO：由后台消费线程完成，不占用调用方。
        /// </summary>
        public void Log(LogLevel level, string message, Exception? ex = null, string? tag = null)
        {
            if (level < MinimumLevel) return;


            // ── 触发 UI 事件（必须在最前面，不受 EnableFileLogging 影响）────
            OnLogEntry?.Invoke(new LogEntry
            {
                Time = DateTime.Now,
                Level = level,
                Tag = tag ?? "",
                Message = string.IsNullOrEmpty(message) ? "(empty)" : message
            });

            // Debug 输出在调用方线程直接输出（不走队列，保证实时性）
            if (EnableDebugOutput)
            {
                var debugEntry = FormatEntry(level, message, ex, tag);
                System.Diagnostics.Debug.WriteLine(debugEntry);
            }

            if (!EnableFileLogging) return;

            // ── 去重 / 限流（在调用方线程判断，避免无效入队）────────────────
            string? entryToWrite = null;
            string? summaryToWrite = null;

            if (DedupWindow > TimeSpan.Zero)
            {
                lock (_dedupLock)
                {
                    var key = $"{level}|{tag}|{message}|{ex?.GetType().Name}";
                    var now = DateTime.Now;
                    bool windowExpired = (now - _dedupWindowStart) > DedupWindow;

                    if (key == _lastDedupKey && !windowExpired)
                    {
                        _dedupCount++;
                        if (_dedupCount > DedupThreshold)
                            return; // 超出阈值，丢弃
                    }
                    else
                    {
                        // 先生成上一条的汇总
                        summaryToWrite = BuildDedupSummary();

                        _lastDedupKey = key;
                        _dedupCount = 1;
                        _dedupWindowStart = now;
                    }
                }
            }

            entryToWrite = FormatEntry(level, message, ex, tag);

            // ── 入队（非阻塞，立即返回）──────────────────────────────────────
            if (summaryToWrite != null)
                _channel.Writer.TryWrite(summaryToWrite);

            _channel.Writer.TryWrite(entryToWrite);
        }

        // ── 后台消费线程 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 后台消费循环：从 Channel 读取条目并写入文件。
        /// Channel 关闭后会把剩余条目全部消费完再退出，确保不丢日志。
        /// </summary>
        private void ConsumeLoop()
        {
            // ReadAllAsync 的同步版本：Channel 关闭后自动退出循环
            var reader = _channel.Reader;

            while (true)
            {
                // 同步等待：有数据立即处理，没数据则阻塞（不占 CPU）
                try
                {
                    // WaitToReadAsync().AsTask().GetAwaiter().GetResult() 是
                    // 在后台线程上安全的同步等待方式
                    if (!reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                        break; // Channel 已完成且无剩余数据，退出

                    while (reader.TryRead(out var entry))
                        WriteToFile(entry);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LogService] 消费线程异常: {ex.Message}");
                }
            }

            // Channel 关闭后把 writer 也关掉
            lock (_dedupLock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        // ── 私有方法 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 构建去重汇总行（不直接写入，返回字符串交给队列）。
        /// 必须在 lock(_dedupLock) 内调用。
        /// </summary>
        private string? BuildDedupSummary()
        {
            if (_dedupCount > DedupThreshold && _lastDedupKey != null)
            {
                var suppressed = _dedupCount - DedupThreshold;
                return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [DEDUP  ]  ↑ 上条消息在 {DedupWindow.TotalSeconds:0}s 内又重复了 {suppressed} 次，已抑制输出。";
            }
            return null;
        }

        /// <summary>
        /// 清理超过 RetainDays 天的旧日志文件。
        /// </summary>
        private void CleanupOldLogs()
        {
            if (RetainDays <= 0) return;

            try
            {
                var cutoff = DateTime.Now.AddDays(-RetainDays);
                int deleted = 0;

                foreach (var file in Directory.GetFiles(_logDirectory, "*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }

                if (deleted > 0)
                    System.Diagnostics.Debug.WriteLine(
                        $"[LogService] 已清理 {deleted} 个超过 {RetainDays} 天的旧日志文件。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LogService] 清理旧日志失败: {ex.Message}");
            }
        }

        private const int MaxStackTraceLength = 2000;

        private static string FormatEntry(LogLevel level, string message, Exception? ex, string? tag)
        {
            message = string.IsNullOrEmpty(message) ? "(empty message)" : message;

            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
            sb.Append($" [{LevelLabel(level),-7}]");

            if (!string.IsNullOrWhiteSpace(tag))
                sb.Append($" [{tag}]");

            sb.Append($" {message}");

            if (ex != null)
            {
                sb.AppendLine();
                sb.Append($"  Exception: {ex.GetType().Name}: {ex.Message ?? "(no message)"}");

                if (ex.StackTrace != null)
                {
                    sb.AppendLine();
                    var stack = ex.StackTrace.Length > MaxStackTraceLength
                        ? ex.StackTrace[..MaxStackTraceLength] + $"... (truncated, {ex.StackTrace.Length} chars total)"
                        : ex.StackTrace;
                    sb.Append($"  StackTrace: {stack}");
                }

                if (ex.InnerException != null)
                {
                    sb.AppendLine();
                    sb.Append($"  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message ?? "(no message)"}");
                }
            }

            return sb.ToString();
        }

        private static string LevelLabel(LogLevel level) => level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Fatal => "FTL",
            _ => "???"
        };

        /// <summary>
        /// 实际写入文件（只在后台消费线程调用，无需加锁）。
        /// </summary>
        private void WriteToFile(string entry)
        {
            try
            {
                var now = DateTime.Now;

                if (_writer != null && _currentLogFilePath != null)
                {
                    bool isNewDay = now.Date > _currentLogDate.Date;
                    bool isOverSize = !isNewDay
                        && new FileInfo(_currentLogFilePath).Length >= MaxFileSizeBytes;

                    if (isNewDay || isOverSize)
                    {
                        _writer.Flush();
                        _writer.Dispose();
                        _writer = null;

                        if (isNewDay) CleanupOldLogs();

                        OpenNewLogFile(now, resetSequence: isNewDay);
                    }
                }

                _writer?.WriteLine(entry);
                _writer?.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogService] 写入日志文件失败: {ex.Message}");
            }
        }

        private int _dailySequence = 1;

        private void OpenNewLogFile(DateTime? date = null, bool resetSequence = true)
        {
            var now = date ?? DateTime.Now;

            if (resetSequence)
                _dailySequence = 1;

            string filePath;
            do
            {
                var suffix = _dailySequence == 1 ? "" : $"_{_dailySequence}";
                var fileName = $"{_logFilePrefix}_{now:yyyyMMdd}{suffix}.log";
                filePath = Path.Combine(_logDirectory, fileName);

                if (File.Exists(filePath) && new FileInfo(filePath).Length >= MaxFileSizeBytes)
                    _dailySequence++;
                else
                    break;
            } while (true);

            _currentLogFilePath = filePath;
            _currentLogDate = now;

            _writer = new StreamWriter(_currentLogFilePath, append: true, Encoding.UTF8)
            {
                AutoFlush = false
            };

            _writer.WriteLine(new string('─', 60));
            _writer.WriteLine($"  Log started at {now:yyyy-MM-dd HH:mm:ss}  |  File: {Path.GetFileName(_currentLogFilePath)}");
            _writer.WriteLine(new string('─', 60));
            _writer.Flush();
        }

        // ── 退出处理 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 进程退出事件（AppDomain.ProcessExit）：强制 flush 队列，防止强杀丢日志。
        /// </summary>
        private void OnProcessExit(object? sender, EventArgs e) => FlushAndStop();

        /// <summary>
        /// 关闭 Channel，等待消费线程把队列里剩余的条目全部写完。
        /// 最多等待 3 秒，超时后强制退出，避免卡住进程退出流程。
        /// </summary>
        private void FlushAndStop()
        {
            _channel.Writer.TryComplete(); // 通知消费线程：不再有新数据
            _consumerThread.Join(TimeSpan.FromSeconds(3)); // 等待消费线程写完
        }

        // ── IDisposable ──────────────────────────────────────────────────────────

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            FlushAndStop();
        }
    }
}