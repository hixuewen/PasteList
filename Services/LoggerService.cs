using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PasteList.Services
{
    /// <summary>
    /// 日志服务实现
    /// </summary>
    public class LoggerService : ILoggerService, IDisposable
    {
        private readonly string _logDirectory;
        private string _logFilePath;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lockObject = new object();
        private readonly LoggingConfiguration _config;
        private bool _disposed = false;

        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinimumLevel
        {
            get => _config.MinimumLevel;
            set => _config.MinimumLevel = value;
        }

        /// <summary>
        /// 日志条目结构
        /// </summary>
        private struct LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
            public string Category { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public LoggerService()
        {
            // 加载配置
            _config = LoggingConfiguration.LoadFromFile();

            // 设置日志目录到程序所在目录
            string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string? assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            _logDirectory = Path.Combine(assemblyDirectory ?? ".", "Logs");

            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);

            // 设置日志文件路径（按日期分文件）
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(_logDirectory, $"PasteList-{today}.log");

            // 初始化日志队列
            _logQueue = new ConcurrentQueue<LogEntry>();

            // 初始化信号量
            _semaphore = new SemaphoreSlim(1, 1);

            // 设置定时刷新器（每5分钟刷新一次，作为备用机制）
            _flushTimer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // 记录日志服务启动
            LogInfo($"日志服务已启动，最小日志级别: {_config.MinimumLevel}");
        }

        /// <summary>
        /// 构造函数（使用自定义配置）
        /// </summary>
        /// <param name="config">日志配置</param>
        public LoggerService(LoggingConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 设置日志目录到程序所在目录
            string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string? assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            _logDirectory = Path.Combine(assemblyDirectory ?? ".", "Logs");

            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);

            // 设置日志文件路径（按日期分文件）
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(_logDirectory, $"PasteList-{today}.log");

            // 初始化日志队列
            _logQueue = new ConcurrentQueue<LogEntry>();

            // 初始化信号量
            _semaphore = new SemaphoreSlim(1, 1);

            // 设置定时刷新器（每5分钟刷新一次，作为备用机制）
            _flushTimer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // 记录日志服务启动
            LogInfo($"日志服务已启动，最小日志级别: {_config.MinimumLevel}");
        }

        /// <summary>
        /// 记录跟踪日志
        /// </summary>
        public void LogTrace(string message, params object[] args)
        {
            LogAsync(LogLevel.Trace, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public void LogDebug(string message, params object[] args)
        {
            LogAsync(LogLevel.Debug, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public void LogInfo(string message, params object[] args)
        {
            LogAsync(LogLevel.Info, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public void LogWarning(string message, params object[] args)
        {
            LogAsync(LogLevel.Warning, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public void LogError(string message, params object[] args)
        {
            LogAsync(LogLevel.Error, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录错误日志（带异常）
        /// </summary>
        public void LogError(Exception exception, string message, params object[] args)
        {
            string fullMessage = args.Length > 0 ? string.Format(message, args) : message;
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Error,
                Message = fullMessage,
                Exception = exception,
                Category = "Error"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录严重错误日志
        /// </summary>
        public void LogCritical(string message, params object[] args)
        {
            LogAsync(LogLevel.Critical, message, args).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录严重错误日志（带异常）
        /// </summary>
        public void LogCritical(Exception exception, string message, params object[] args)
        {
            string fullMessage = args.Length > 0 ? string.Format(message, args) : message;
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Critical,
                Message = fullMessage,
                Exception = exception,
                Category = "Critical"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录用户操作日志
        /// </summary>
        public void LogUserAction(string action, string details = "")
        {
            if (!_config.EnableUserActionLogging) return;

            string message = string.IsNullOrEmpty(details) ? $"用户操作: {action}" : $"用户操作: {action} - {details}";
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = message,
                Category = "UserAction"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录剪贴板操作日志
        /// </summary>
        public void LogClipboardOperation(string operation, string contentType, int contentLength, string contentPreview = "")
        {
            if (!_config.EnableClipboardLogging) return;

            string message = string.IsNullOrEmpty(contentPreview)
                ? $"剪贴板操作: {operation} | 类型: {contentType} | 长度: {contentLength}"
                : $"剪贴板操作: {operation} | 类型: {contentType} | 长度: {contentLength} | 内容: {contentPreview}";

            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Debug,
                Message = message,
                Category = "Clipboard"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录应用程序启动日志
        /// </summary>
        public void LogApplicationStart()
        {
            string message = $@"应用程序启动 | 版本: {GetApplicationVersion()} | 系统: {Environment.OSVersion} | 用户: {Environment.UserName}";
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = message,
                Category = "Application"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录应用程序关闭日志
        /// </summary>
        public void LogApplicationShutdown()
        {
            string message = "应用程序关闭";
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = message,
                Category = "Application"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 记录性能日志
        /// </summary>
        public void LogPerformance(string operation, long duration, string details = "")
        {
            if (!_config.EnablePerformanceLogging) return;

            string message = string.IsNullOrEmpty(details)
                ? $"性能: {operation} 耗时 {duration}ms"
                : $"性能: {operation} 耗时 {duration}ms | {details}";
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Debug,
                Message = message,
                Category = "Performance"
            };
            EnqueueLog(entry);
        }

        /// <summary>
        /// 异步记录日志
        /// </summary>
        public async Task LogAsync(LogLevel level, string message, params object[] args)
        {
            if (level < MinimumLevel) return;

            string fullMessage = args.Length > 0 ? string.Format(message, args) : message;
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = fullMessage,
                Category = "General"
            };

            EnqueueLog(entry);

            // 控制台输出
            if (_config.EnableConsoleOutput)
            {
                string consoleMessage = FormatLogEntry(entry);
                Console.WriteLine(consoleMessage);
            }

            // 立即刷新所有日志，确保及时性
            await FlushAsync();
        }

        /// <summary>
        /// 将日志条目加入队列
        /// </summary>
        private void EnqueueLog(LogEntry entry)
        {
            if (_disposed) return;

            _logQueue.Enqueue(entry);

            // 如果队列太大，立即刷新
            if (_logQueue.Count > 100)
            {
                Task.Run(async () => await FlushAsync());
            }
        }

        /// <summary>
        /// 刷新日志缓冲区到文件
        /// </summary>
        public async Task FlushAsync()
        {
            if (_disposed || _logQueue.IsEmpty || !_config.EnableFileOutput) return;

            await _semaphore.WaitAsync();
            try
            {
                var logs = new List<LogEntry>();

                // 批量取出日志条目
                while (_logQueue.TryDequeue(out LogEntry entry))
                {
                    logs.Add(entry);
                }

                if (logs.Count == 0) return;

                // 检查是否需要切换日志文件（跨天）
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                string expectedFileName = $"PasteList-{today}.log";
                string expectedFilePath = Path.Combine(_logDirectory, expectedFileName);

                if (_logFilePath != expectedFilePath)
                {
                    _logFilePath = expectedFilePath;
                }

                // 写入日志文件
                await WriteLogsToFile(logs, _logFilePath);
            }
            catch (Exception ex)
            {
                // 日志写入失败，输出到Debug
                System.Diagnostics.Debug.WriteLine($"写入日志文件失败: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 写入日志到文件
        /// </summary>
        private async Task WriteLogsToFile(List<LogEntry> logs, string filePath)
        {
            var sb = new StringBuilder();

            foreach (var log in logs)
            {
                string logLine = FormatLogEntry(log);
                sb.AppendLine(logLine);
            }

            // 异步写入文件
            using (var writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                await writer.WriteAsync(sb.ToString());
                await writer.FlushAsync();
            }

            // 清理旧日志文件（保留最近30天）
            CleanOldLogs();
        }

        /// <summary>
        /// 格式化日志条目
        /// </summary>
        private string FormatLogEntry(LogEntry entry)
        {
            string levelStr = entry.Level.ToString().ToUpper();
            string categoryStr = string.IsNullOrEmpty(entry.Category) ? "" : $"[{entry.Category}] ";

            string message = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} {levelStr} {categoryStr}{entry.Message}";

            if (entry.Exception != null)
            {
                message += $"\nException: {entry.Exception.GetType().Name}: {entry.Exception.Message}";
                message += $"\nStackTrace: {entry.Exception.StackTrace}";
            }

            return message;
        }

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        private void CleanOldLogs()
        {
            try
            {
                var files = Directory.GetFiles(_logDirectory, "PasteList-*.log");
                var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取应用程序版本
        /// </summary>
        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // 停止定时器
            _flushTimer?.Dispose();

            // 最后一次刷新日志
            FlushAsync().GetAwaiter().GetResult();

            // 释放信号量
            _semaphore?.Dispose();

            LogInfo("日志服务已关闭");
        }
    }
}
