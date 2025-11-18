using PasteList.Data;
using PasteList.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 同步配置服务实现类
    /// </summary>
    public class SyncConfigurationService : ISyncConfigurationService
    {
        private readonly ClipboardDbContext _dbContext;
        private readonly ILoggerService _loggerService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="loggerService">日志服务</param>
        public SyncConfigurationService(ClipboardDbContext dbContext, ILoggerService loggerService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        /// <summary>
        /// 获取当前同步配置
        /// </summary>
        public async Task<SyncConfiguration> GetCurrentConfigurationAsync()
        {
            try
            {
                var config = await _dbContext.SyncConfigurations
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                if (config == null)
                {
                    // 如果没有配置，创建默认配置
                    _loggerService.LogInfo("未找到同步配置，创建默认配置");
                    config = await InitializeDefaultConfigurationAsync();
                }

                _loggerService.LogDebug($"成功获取同步配置: 启用={config.IsEnabled}, 类型={config.SyncType}");
                return config;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"获取同步配置失败: {ex.Message}", ex);

                // 检查是否是表不存在的错误
                if (ex.Message.Contains("no such table") || ex.Message.Contains("no such table: sync_configurations"))
                {
                    _loggerService.LogError("sync_configurations 表不存在，这通常意味着数据库迁移未正确执行");
                    throw new InvalidOperationException("数据库表 sync_configurations 不存在。请重新启动应用程序以自动创建该表。", ex);
                }

                // 返回默认配置而不是抛出异常
                return new SyncConfiguration("LocalFile", false);
            }
        }

        /// <summary>
        /// 保存同步配置
        /// </summary>
        public async Task<bool> SaveConfigurationAsync(SyncConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            try
            {
                // 验证配置
                var (isValid, errorMessage) = await ValidateConfigurationAsync(configuration);
                if (!isValid)
                {
                    _loggerService.LogError($"配置验证失败: {errorMessage}");
                    return false;
                }

                // 检查是否已存在配置
                var existingConfig = await _dbContext.SyncConfigurations
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

                if (existingConfig != null)
                {
                    // 更新现有配置 - 直接修改现有对象而不是创建新对象
                    existingConfig.SyncType = configuration.SyncType;
                    existingConfig.IsEnabled = configuration.IsEnabled;
                    existingConfig.ConfigData = configuration.ConfigData;
                    existingConfig.LastSyncTime = configuration.LastSyncTime;
                    existingConfig.UpdatedAt = DateTime.UtcNow;

                    // 确保实体状态为已修改
                    _dbContext.Entry(existingConfig).State = EntityState.Modified;
                }
                else
                {
                    // 添加新配置
                    configuration.CreatedAt = DateTime.UtcNow;
                    configuration.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SyncConfigurations.AddAsync(configuration);
                }

                var result = await _dbContext.SaveChangesAsync();

                _loggerService.LogInfo($"同步配置保存成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"保存同步配置失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public async Task<int> UpdateConfigurationAsync(SyncConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            try
            {
                var existingConfig = await _dbContext.SyncConfigurations
                    .FirstOrDefaultAsync(c => c.Id == configuration.Id);

                if (existingConfig == null)
                {
                    _loggerService.LogWarning($"未找到ID为 {configuration.Id} 的同步配置");
                    return 0;
                }

                configuration.CreatedAt = existingConfig.CreatedAt;
                configuration.UpdatedAt = DateTime.UtcNow;

                _dbContext.Entry(existingConfig).CurrentValues.SetValues(configuration);
                var result = await _dbContext.SaveChangesAsync();

                _loggerService.LogInfo($"同步配置更新成功，影响行数: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"更新同步配置失败: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// 删除配置
        /// </summary>
        public async Task<bool> DeleteConfigurationAsync(int id)
        {
            try
            {
                var config = await _dbContext.SyncConfigurations
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (config == null)
                {
                    _loggerService.LogWarning($"未找到ID为 {id} 的同步配置");
                    return false;
                }

                _dbContext.SyncConfigurations.Remove(config);
                var result = await _dbContext.SaveChangesAsync();

                _loggerService.LogInfo($"同步配置删除成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"删除同步配置失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 初始化默认配置
        /// </summary>
        public async Task<SyncConfiguration> InitializeDefaultConfigurationAsync()
        {
            var defaultConfig = new SyncConfiguration("LocalFile", false)
            {
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _dbContext.SyncConfigurations.AddAsync(defaultConfig);
                await _dbContext.SaveChangesAsync();

                _loggerService.LogInfo("默认同步配置初始化成功");
                return defaultConfig;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"初始化默认同步配置失败: {ex.Message}", ex);
                // 返回未保存的配置
                return defaultConfig;
            }
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public async Task<(bool IsValid, string? ErrorMessage)> ValidateConfigurationAsync(SyncConfiguration configuration)
        {
            if (configuration == null)
                return (false, "配置不能为空");

            // 验证同步类型
            if (string.IsNullOrEmpty(configuration.SyncType))
                return (false, "同步类型不能为空");

            // 检查同步类型是否支持
            var supportedTypes = new[] { "LocalFile", "OneDrive", "GoogleDrive" };
            if (!supportedTypes.Contains(configuration.SyncType))
                return (false, $"不支持的同步类型: {configuration.SyncType}");

            // 验证配置数据（如果是特定类型）
            if (configuration.SyncType == "LocalFile")
            {
                // 本地文件同步不需要额外验证
                return (true, null);
            }
            else if (configuration.SyncType == "OneDrive" || configuration.SyncType == "GoogleDrive")
            {
                // 云端同步需要配置数据
                if (string.IsNullOrEmpty(configuration.ConfigData))
                    return (false, "云端同步需要配置数据");
            }

            return (true, null);
        }

        /// <summary>
        /// 更新最后同步时间
        /// </summary>
        public async Task<bool> UpdateLastSyncTimeAsync()
        {
            try
            {
                var config = await GetCurrentConfigurationAsync();
                config.LastSyncTime = DateTime.UtcNow;
                config.UpdatedAt = DateTime.UtcNow;

                var result = await _dbContext.SaveChangesAsync();

                _loggerService.LogInfo($"最后同步时间已更新: {config.LastSyncTime}");
                return result > 0;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"更新最后同步时间失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 检查是否启用了同步
        /// </summary>
        public async Task<bool> IsSyncEnabledAsync()
        {
            try
            {
                var config = await GetCurrentConfigurationAsync();
                return config.IsEnabled;
            }
            catch
            {
                return false;
            }
        }
    }
}
