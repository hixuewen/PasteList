using System;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 自动同步服务接口
    /// </summary>
    public interface IAutoSyncService : IDisposable
    {
        /// <summary>
        /// 同步状态变化事件
        /// </summary>
        event EventHandler<AutoSyncStatusEventArgs>? StatusChanged;

        /// <summary>
        /// 启动自动同步服务
        /// </summary>
        /// <returns>异步任务</returns>
        Task StartAsync();

        /// <summary>
        /// 停止自动同步服务
        /// </summary>
        void Stop();

        /// <summary>
        /// 剪贴板变化时触发同步
        /// </summary>
        /// <returns>异步任务</returns>
        Task OnClipboardChangedAsync();

        /// <summary>
        /// 手动触发同步
        /// </summary>
        /// <param name="reason">同步原因</param>
        /// <returns>异步任务</returns>
        Task ManualSyncAsync(string reason = "手动同步");

        /// <summary>
        /// 获取当前同步状态
        /// </summary>
        /// <returns>同步状态</returns>
        AutoSyncStatus GetCurrentStatus();
    }

    /// <summary>
    /// 自动同步状态
    /// </summary>
    public class AutoSyncStatus
    {
        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 是否正在同步
        /// </summary>
        public bool IsSyncing { get; set; }

        /// <summary>
        /// 当前消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 最后同步时间
        /// </summary>
        public DateTime? LastSyncTime { get; set; }

        /// <summary>
        /// 下次同步时间
        /// </summary>
        public DateTime? NextSyncTime { get; set; }

        /// <summary>
        /// 同步错误次数
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 最后错误信息
        /// </summary>
        public string? LastError { get; set; }
    }

    /// <summary>
    /// 自动同步状态事件参数
    /// </summary>
    public class AutoSyncStatusEventArgs : EventArgs
    {
        /// <summary>
        /// 状态消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 是否正在同步
        /// </summary>
        public bool IsSyncing { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">状态消息</param>
        /// <param name="isSyncing">是否正在同步</param>
        public AutoSyncStatusEventArgs(string message, bool isSyncing)
        {
            Message = message;
            IsSyncing = isSyncing;
        }
    }
}