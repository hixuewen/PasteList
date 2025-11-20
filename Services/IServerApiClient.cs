using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 服务器API客户端接口
    /// </summary>
    public interface IServerApiClient : IDisposable
    {
        /// <summary>
        /// 服务器配置
        /// </summary>
        ServerSyncConfig Config { get; }

        /// <summary>
        /// 验证与服务器的连接
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否有效</returns>
        Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 推送剪贴板数据到服务器
        /// </summary>
        /// <param name="items">剪贴板项目列表</param>
        /// <param name="deviceId">设备ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>推送结果</returns>
        Task<PushResult> PushItemsAsync(List<ClipboardItem> items, string deviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从服务器拉取剪贴板数据
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="lastSyncTime">最后同步时间（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器端剪贴板项目列表</returns>
        Task<List<ClipboardItem>> PullItemsAsync(string deviceId, DateTime? lastSyncTime = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 双向同步（合并数据）
        /// </summary>
        /// <param name="items">本地剪贴板项目</param>
        /// <param name="deviceId">设备ID</param>
        /// <param name="lastSyncTime">最后同步时间</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>同步结果和服务器端数据</returns>
        Task<BidirectionalSyncResult> BidirectionalSyncAsync(List<ClipboardItem> items, string deviceId, DateTime? lastSyncTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>服务器状态</returns>
        Task<ServerStatus> GetServerStatusAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 推送结果
    /// </summary>
    public class PushResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 推送的记录数量
        /// </summary>
        public int PushedCount { get; set; }

        /// <summary>
        /// 跳过的重复记录数量
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// 服务器端最新时间戳
        /// </summary>
        public DateTime? ServerTimestamp { get; set; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 成功构造函数
        /// </summary>
        public PushResult()
        {
        }

        /// <summary>
        /// 成功构造函数
        /// </summary>
        /// <param name="pushedCount">推送数量</param>
        /// <param name="skippedCount">跳过数量</param>
        /// <param name="serverTimestamp">服务器时间戳</param>
        public PushResult(int pushedCount, int skippedCount, DateTime? serverTimestamp)
        {
            Success = true;
            PushedCount = pushedCount;
            SkippedCount = skippedCount;
            ServerTimestamp = serverTimestamp;
        }

        /// <summary>
        /// 失败构造函数
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        public PushResult(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 双向同步结果
    /// </summary>
    public class BidirectionalSyncResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 服务器端返回的剪贴板项目
        /// </summary>
        public List<ClipboardItem> ServerItems { get; set; } = new List<ClipboardItem>();

        /// <summary>
        /// 解决的冲突数量
        /// </summary>
        public int ConflictsResolved { get; set; }

        /// <summary>
        /// 推送到服务器的记录数
        /// </summary>
        public int PushedCount { get; set; }

        /// <summary>
        /// 拉取的记录数
        /// </summary>
        public int PulledCount { get; set; }

        /// <summary>
        /// 服务器端最新时间戳
        /// </summary>
        public DateTime? ServerTimestamp { get; set; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 成功构造函数
        /// </summary>
        public BidirectionalSyncResult()
        {
        }

        /// <summary>
        /// 成功构造函数
        /// </summary>
        /// <param name="serverItems">服务器端数据</param>
        /// <param name="conflictsResolved">冲突解决数量</param>
        /// <param name="pushedCount">推送数量</param>
        /// <param name="pulledCount">拉取数量</param>
        /// <param name="serverTimestamp">服务器时间戳</param>
        public BidirectionalSyncResult(List<ClipboardItem> serverItems, int conflictsResolved, int pushedCount, int pulledCount, DateTime? serverTimestamp)
        {
            Success = true;
            ServerItems = serverItems;
            ConflictsResolved = conflictsResolved;
            PushedCount = pushedCount;
            PulledCount = pulledCount;
            ServerTimestamp = serverTimestamp;
        }

        /// <summary>
        /// 失败构造函数
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        public BidirectionalSyncResult(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 服务器状态信息
    /// </summary>
    public class ServerStatus
    {
        /// <summary>
        /// 服务器名称
        /// </summary>
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// 服务器版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 服务器时间
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// 状态是否健康
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// 错误信息（如果不健康）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 服务器端支持的功能列表
        /// </summary>
        public List<string> SupportedFeatures { get; set; } = new List<string>();
    }
}
