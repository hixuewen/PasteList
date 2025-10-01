using System;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// 日志服务接口
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// 记录跟踪日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogTrace(string message, params object[] args);

        /// <summary>
        /// 记录调试日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogDebug(string message, params object[] args);

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogInfo(string message, params object[] args);

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogWarning(string message, params object[] args);

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogError(string message, params object[] args);

        /// <summary>
        /// 记录错误日志（带异常）
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogError(Exception exception, string message, params object[] args);

        /// <summary>
        /// 记录严重错误日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogCritical(string message, params object[] args);

        /// <summary>
        /// 记录严重错误日志（带异常）
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        void LogCritical(Exception exception, string message, params object[] args);

        /// <summary>
        /// 记录用户操作日志
        /// </summary>
        /// <param name="action">操作名称</param>
        /// <param name="details">操作详情</param>
        void LogUserAction(string action, string details = "");

        /// <summary>
        /// 记录剪贴板操作日志
        /// </summary>
        /// <param name="operation">操作类型</param>
        /// <param name="contentType">内容类型</param>
        /// <param name="contentLength">内容长度</param>
        /// <param name="contentPreview">内容预览（可选）</param>
        void LogClipboardOperation(string operation, string contentType, int contentLength, string contentPreview = "");

        /// <summary>
        /// 记录应用程序启动日志
        /// </summary>
        void LogApplicationStart();

        /// <summary>
        /// 记录应用程序关闭日志
        /// </summary>
        void LogApplicationShutdown();

        /// <summary>
        /// 记录性能日志
        /// </summary>
        /// <param name="operation">操作名称</param>
        /// <param name="duration">耗时（毫秒）</param>
        /// <param name="details">详细信息</param>
        void LogPerformance(string operation, long duration, string details = "");

        /// <summary>
        /// 异步记录日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="args">格式化参数</param>
        Task LogAsync(LogLevel level, string message, params object[] args);

        /// <summary>
        /// 刷新日志缓冲区
        /// </summary>
        Task FlushAsync();

        /// <summary>
        /// 获取或设置最小日志级别
        /// </summary>
        LogLevel MinimumLevel { get; set; }
    }
}