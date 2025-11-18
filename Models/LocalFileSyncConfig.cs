using System;

namespace PasteList.Models
{
    /// <summary>
    /// 本地文件同步配置
    /// </summary>
    public class LocalFileSyncConfig
    {
        /// <summary>
        /// 同步文件夹路径
        /// </summary>
        public string SyncFolderPath { get; set; } = string.Empty;

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
        /// 最大备份文件数
        /// </summary>
        public int MaxBackupFiles { get; set; } = 5;

        /// <summary>
        /// 最后自动同步时间
        /// </summary>
        public DateTime? LastAutoSyncTime { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public LocalFileSyncConfig()
        {
            // 设置默认同步文件夹为用户文档文件夹
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            SyncFolderPath = System.IO.Path.Combine(documentsFolder, "PasteListSync");
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        /// <returns>验证结果和错误信息</returns>
        public (bool IsValid, string? ErrorMessage) Validate()
        {
            if (string.IsNullOrWhiteSpace(SyncFolderPath))
            {
                return (false, "同步文件夹路径不能为空");
            }

            if (SyncIntervalMinutes < 1 || SyncIntervalMinutes > 60)
            {
                return (false, "同步间隔必须在 1-60 分钟之间");
            }

            if (MaxBackupFiles < 1 || MaxBackupFiles > 20)
            {
                return (false, "最大备份文件数必须在 1-20 之间");
            }

            return (true, null);
        }
    }

    /// <summary>
    /// 冲突解决策略枚举
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>
        /// 保留较新的版本
        /// </summary>
        KeepNewer,

        /// <summary>
        /// 保留两个版本
        /// </summary>
        KeepBoth,

        /// <summary>
        /// 保留本地版本
        /// </summary>
        KeepLocal,

        /// <summary>
        /// 保留远程版本
        /// </summary>
        KeepRemote
    }
}