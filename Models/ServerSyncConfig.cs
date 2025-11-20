using System;
using System.ComponentModel.DataAnnotations;

namespace PasteList.Models
{
    /// <summary>
    /// 服务器同步配置
    /// </summary>
    public class ServerSyncConfig
    {
        /// <summary>
        /// 服务器地址（如：http://localhost:5000）
        /// </summary>
        [Required]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// 设备唯一标识符
        /// </summary>
        public string DeviceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 同步方向
        /// </summary>
        public SyncDirection SyncDirection { get; set; } = SyncDirection.Bidirectional;

        /// <summary>
        /// 同步间隔（分钟）
        /// </summary>
        public int SyncIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 剪贴板变化时自动同步
        /// </summary>
        public bool AutoSyncOnClipboardChange { get; set; } = true;

        /// <summary>
        /// 启用冲突检测和解决
        /// </summary>
        public bool EnableConflictResolution { get; set; } = true;

        /// <summary>
        /// 冲突解决策略
        /// </summary>
        public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.KeepNewer;

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 最后自动同步时间
        /// </summary>
        public DateTime? LastAutoSyncTime { get; set; }

        /// <summary>
        /// 最后同步时间（与服务器）
        /// </summary>
        public DateTime? LastSyncTime { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ServerSyncConfig()
        {
            // 生成唯一设备ID
            DeviceId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        public (bool IsValid, string? ErrorMessage) Validate()
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
            {
                return (false, "服务器地址不能为空");
            }

            // 验证服务器URL格式
            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "服务器地址格式无效，请使用 http:// 或 https:// 开头");
            }

            if (SyncIntervalMinutes < 1 || SyncIntervalMinutes > 60)
            {
                return (false, "同步间隔必须在 1-60 分钟之间");
            }

            if (ConnectionTimeoutSeconds < 5 || ConnectionTimeoutSeconds > 120)
            {
                return (false, "连接超时时间必须在 5-120 秒之间");
            }

            if (MaxRetryAttempts < 0 || MaxRetryAttempts > 10)
            {
                return (false, "重试次数必须在 0-10 之间");
            }

            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                return (false, "设备ID不能为空");
            }

            return (true, null);
        }

        /// <summary>
        /// 获取完整的API基础URL
        /// </summary>
        /// <returns>API基础URL</returns>
        public string GetApiBaseUrl()
        {
            var baseUrl = ServerUrl.TrimEnd('/');
            return $"{baseUrl}/api";
        }
    }

    /// <summary>
    /// 同步方向枚举
    /// </summary>
    public enum SyncDirection
    {
        /// <summary>
        /// 仅推送到服务器
        /// </summary>
        PushOnly,

        /// <summary>
        /// 仅从服务器拉取
        /// </summary>
        PullOnly,

        /// <summary>
        /// 双向同步
        /// </summary>
        Bidirectional
    }
}
