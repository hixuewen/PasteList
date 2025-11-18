using PasteList.Models;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 同步配置服务接口
    /// </summary>
    public interface ISyncConfigurationService
    {
        /// <summary>
        /// 获取当前同步配置
        /// </summary>
        /// <returns>同步配置，如果不存在则返回默认配置</returns>
        Task<SyncConfiguration> GetCurrentConfigurationAsync();

        /// <summary>
        /// 保存同步配置
        /// </summary>
        /// <param name="configuration">要保存的配置</param>
        /// <returns>是否保存成功</returns>
        Task<bool> SaveConfigurationAsync(SyncConfiguration configuration);

        /// <summary>
        /// 更新配置
        /// </summary>
        /// <param name="configuration">配置</param>
        /// <returns>影响的行数</returns>
        Task<int> UpdateConfigurationAsync(SyncConfiguration configuration);

        /// <summary>
        /// 删除配置
        /// </summary>
        /// <param name="id">配置ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteConfigurationAsync(int id);

        /// <summary>
        /// 初始化默认配置
        /// </summary>
        /// <returns>默认配置</returns>
        Task<SyncConfiguration> InitializeDefaultConfigurationAsync();

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <param name="configuration">要验证的配置</param>
        /// <returns>验证结果</returns>
        Task<(bool IsValid, string? ErrorMessage)> ValidateConfigurationAsync(SyncConfiguration configuration);

        /// <summary>
        /// 更新最后同步时间
        /// </summary>
        /// <returns>是否更新成功</returns>
        Task<bool> UpdateLastSyncTimeAsync();

        /// <summary>
        /// 检查是否启用了同步
        /// </summary>
        /// <returns>如果启用返回true，否则返回false</returns>
        Task<bool> IsSyncEnabledAsync();
    }
}
