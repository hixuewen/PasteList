using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 服务器同步服务接口
    /// </summary>
    public interface IServerSyncService
    {
        /// <summary>
        /// 同步进度变化事件
        /// </summary>
        event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// 同步完成事件
        /// </summary>
        event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// 验证服务器连接
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否有效</returns>
        Task<bool> ValidateConnectionAsync(ServerSyncConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 将本地剪贴板数据推送到服务器
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>推送的记录数量</returns>
        Task<int> PushAsync(ServerSyncConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从服务器拉取剪贴板数据
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>拉取的记录数量</returns>
        Task<int> PullAsync(ServerSyncConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 双向同步（在本地和服务器之间同步数据）
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>同步的记录总数量</returns>
        Task<SyncResult> BidirectionalSyncAsync(ServerSyncConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取同步历史记录
        /// </summary>
        /// <param name="count">获取数量</param>
        /// <returns>同步历史列表</returns>
        Task<List<ServerSyncHistoryEntry>> GetSyncHistoryAsync(int count = 10);

        /// <summary>
        /// 清理同步历史
        /// </summary>
        /// <param name="beforeDate">清理此日期之前的记录</param>
        /// <returns>清理的记录数量</returns>
        Task<int> CleanSyncHistoryAsync(DateTime beforeDate);
    }

    /// <summary>
    /// 双向同步结果
    /// </summary>
    public class SyncResult
    {
        /// <summary>
        /// 推送到服务器的记录数量
        /// </summary>
        public int PushedCount { get; set; }

        /// <summary>
        /// 从服务器拉取的记录数量
        /// </summary>
        public int PulledCount { get; set; }

        /// <summary>
        /// 解决的冲突数量
        /// </summary>
        public int ConflictsResolved { get; set; }

        /// <summary>
        /// 同步是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 最后同步时间
        /// </summary>
        public DateTime LastSyncTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SyncResult()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pushedCount">推送数量</param>
        /// <param name="pulledCount">拉取数量</param>
        /// <param name="conflictsResolved">冲突解决数量</param>
        public SyncResult(int pushedCount, int pulledCount, int conflictsResolved)
        {
            PushedCount = pushedCount;
            PulledCount = pulledCount;
            ConflictsResolved = conflictsResolved;
            Success = true;
        }

        /// <summary>
        /// 失败构造函数
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        public SyncResult(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 获取总同步记录数
        /// </summary>
        public int TotalRecords => PushedCount + PulledCount;
    }

    /// <summary>
    /// 服务器同步历史记录条目
    /// </summary>
    public class ServerSyncHistoryEntry
    {
        /// <summary>
        /// 操作时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 操作类型（推送/拉取/双向同步）
        /// </summary>
        public ServerSyncOperationType OperationType { get; set; }

        /// <summary>
        /// 记录数量
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// 推送到服务器的记录数（双向同步时）
        /// </summary>
        public int? PushedCount { get; set; }

        /// <summary>
        /// 从服务器拉取的记录数（双向同步时）
        /// </summary>
        public int? PulledCount { get; set; }

        /// <summary>
        /// 解决的冲突数（双向同步时）
        /// </summary>
        public int? ConflictsResolved { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string? ServerUrl { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        public string? DeviceId { get; set; }
    }

    /// <summary>
    /// 服务器同步操作类型
    /// </summary>
    public enum ServerSyncOperationType
    {
        /// <summary>
        /// 推送数据到服务器
        /// </summary>
        Push,

        /// <summary>
        /// 从服务器拉取数据
        /// </summary>
        Pull,

        /// <summary>
        /// 双向同步
        /// </summary>
        Bidirectional,

        /// <summary>
        /// 验证连接
        /// </summary>
        Validate
    }
}
