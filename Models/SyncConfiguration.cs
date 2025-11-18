using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasteList.Models
{
    /// <summary>
    /// 同步配置数据模型
    /// </summary>
    [Table("sync_configurations")]
    public class SyncConfiguration
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// 同步方式类型（如：LocalFile, OneDrive等）
        /// </summary>
        [Required]
        [Column("sync_type")]
        public string SyncType { get; set; } = "LocalFile";

        /// <summary>
        /// 是否启用同步
        /// </summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 同步配置参数（JSON格式存储，如文件路径、云端配置等）
        /// </summary>
        [Column("config_data")]
        public string? ConfigData { get; set; }

        /// <summary>
        /// 最后同步时间
        /// </summary>
        [Column("last_sync_time")]
        public DateTime? LastSyncTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SyncConfiguration()
        {
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        /// <param name="syncType">同步类型</param>
        /// <param name="isEnabled">是否启用</param>
        public SyncConfiguration(string syncType, bool isEnabled = false) : this()
        {
            SyncType = syncType;
            IsEnabled = isEnabled;
        }
    }
}