using System;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 应用设置服务接口
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 是否启用同步
        /// </summary>
        bool IsSyncEnabled { get; set; }

        /// <summary>
        /// 服务器地址
        /// </summary>
        string ServerUrl { get; set; }

        /// <summary>
        /// 加载设置
        /// </summary>
        Task LoadAsync();

        /// <summary>
        /// 保存设置
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 同步设置变化事件
        /// </summary>
        event EventHandler<bool>? SyncEnabledChanged;
    }
}

