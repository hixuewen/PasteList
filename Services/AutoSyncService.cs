using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PasteList.Models;

namespace PasteList.Services
{
    /// <summary>
    /// 自动同步服务实现类
    /// </summary>
    public class AutoSyncService : IAutoSyncService
    {
        private readonly ISyncService _syncService;
        private readonly ISyncConfigurationService _configService;
        private readonly IClipboardHistoryService _historyService;
        private readonly ILoggerService _loggerService;
        private readonly Timer? _syncTimer;
        private DateTime _lastClipboardChangeTime = DateTime.MinValue;
        private readonly object _syncLock = new object();
        private bool _isSyncing = false;
        private bool _isDisposed = false;
        private AutoSyncStatus _status = new();

        /// <summary>
        /// 同步状态变化事件
        /// </summary>
        public event EventHandler<AutoSyncStatusEventArgs>? StatusChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AutoSyncService(
            ISyncService syncService,
            ISyncConfigurationService configService,
            IClipboardHistoryService historyService,
            ILoggerService loggerService)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));

            // 初始化定时器
            _syncTimer = new Timer(OnSyncTimer, null, Timeout.Infinite, Timeout.Infinite);

            // 订阅同步服务事件
            _syncService.SyncCompleted += OnSyncCompleted;
        }

        /// <summary>
        /// 启动自动同步服务
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                var config = await _configService.GetCurrentConfigurationAsync();
                if (config?.IsEnabled == true && config.SyncType == "LocalFile")
                {
                    var localConfig = GetLocalFileConfig(config);
                    _syncTimer?.Change(TimeSpan.FromMinutes(localConfig.SyncIntervalMinutes),
                                     TimeSpan.FromMinutes(localConfig.SyncIntervalMinutes));

                    _status.IsRunning = true;
                    _status.NextSyncTime = DateTime.UtcNow.AddMinutes(localConfig.SyncIntervalMinutes);

                    OnStatusChanged(new AutoSyncStatusEventArgs("自动同步已启动", false));
                    _loggerService.LogInfo("自动同步服务已启动");
                }
                else
                {
                    _status.IsRunning = false;
                    _loggerService.LogInfo("同步功能未启用，自动同步服务未启动");
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "启动自动同步服务失败");
                _status.IsRunning = false;
                OnStatusChanged(new AutoSyncStatusEventArgs($"启动失败: {ex.Message}", false));
            }
        }

        /// <summary>
        /// 停止自动同步服务
        /// </summary>
        public void Stop()
        {
            try
            {
                _syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _status.IsRunning = false;
                _status.NextSyncTime = null;

                OnStatusChanged(new AutoSyncStatusEventArgs("自动同步已停止", false));
                _loggerService.LogInfo("自动同步服务已停止");
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "停止自动同步服务失败");
            }
        }

        /// <summary>
        /// 剪贴板变化时触发同步
        /// </summary>
        public async Task OnClipboardChangedAsync()
        {
            if (_isSyncing || _isDisposed) return;

            try
            {
                var config = await _configService.GetCurrentConfigurationAsync();
                if (config?.IsEnabled == true &&
                    config.SyncType == "LocalFile" &&
                    GetLocalFileConfig(config).AutoSyncOnClipboardChange)
                {
                    _lastClipboardChangeTime = DateTime.UtcNow;

                    // 延迟3秒执行同步，避免剪贴板频繁变化
                    await Task.Delay(3000);

                    if (DateTime.UtcNow.Subtract(_lastClipboardChangeTime).TotalSeconds >= 3)
                    {
                        await PerformAutoSyncAsync("剪贴板变化同步");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "处理剪贴板变化时发生错误");
            }
        }

        /// <summary>
        /// 手动触发同步
        /// </summary>
        public async Task ManualSyncAsync(string reason = "手动同步")
        {
            await PerformAutoSyncAsync(reason);
        }

        /// <summary>
        /// 获取当前同步状态
        /// </summary>
        public AutoSyncStatus GetCurrentStatus()
        {
            // 确保 _status.IsSyncing 与内部 _isSyncing 字段保持同步
            _status.IsSyncing = _isSyncing;
            return _status;
        }

        /// <summary>
        /// 定时器回调
        /// </summary>
        private async void OnSyncTimer(object? state)
        {
            if (_isSyncing || _isDisposed) return;

            await PerformAutoSyncAsync("定时同步");
        }

        /// <summary>
        /// 执行自动同步
        /// </summary>
        private async Task PerformAutoSyncAsync(string reason)
        {
            if (_isSyncing || _isDisposed)
            {
                _loggerService?.LogDebug($"同步被忽略: 已在进行中或已释放");
                return;
            }

            lock (_syncLock)
            {
                if (_isSyncing)
                {
                    _loggerService?.LogDebug("同步被忽略: 锁检查失败");
                    return;
                }
                _isSyncing = true;
            }

            try
            {
                var config = await _configService.GetCurrentConfigurationAsync();
                if (config?.IsEnabled != true)
                {
                    _loggerService?.LogDebug("同步被忽略: 同步功能未启用");
                    OnStatusChanged(new AutoSyncStatusEventArgs("同步功能未启用", false));
                    return;
                }

                if (config.SyncType != "LocalFile")
                {
                    _loggerService?.LogDebug($"同步被忽略: 当前同步类型为 {config.SyncType}，仅支持 LocalFile 类型");
                    OnStatusChanged(new AutoSyncStatusEventArgs($"当前同步类型 ({config.SyncType}) 不支持手动同步", false));
                    return;
                }

                var localConfig = GetLocalFileConfig(config);
                if (string.IsNullOrEmpty(localConfig.SyncFolderPath))
                {
                    _loggerService?.LogDebug("同步被忽略: 同步文件夹路径未配置");
                    OnStatusChanged(new AutoSyncStatusEventArgs("同步文件夹路径未配置", false));
                    return;
                }

                OnStatusChanged(new AutoSyncStatusEventArgs($"正在执行{reason}...", true));
                _status.IsSyncing = true;

                var syncFilePath = Path.Combine(localConfig.SyncFolderPath, "pasteList_sync.json");
                var backupFilePath = Path.Combine(localConfig.SyncFolderPath,
                    $"pasteList_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");

                // 确保同步文件夹存在
                if (!Directory.Exists(localConfig.SyncFolderPath))
                {
                    Directory.CreateDirectory(localConfig.SyncFolderPath);
                }

                // 创建备份
                if (File.Exists(syncFilePath))
                {
                    try
                    {
                        File.Copy(syncFilePath, backupFilePath, true);
                        await CleanupOldBackupsAsync(localConfig);
                        _loggerService.LogDebug($"创建备份文件: {backupFilePath}");
                    }
                    catch (Exception ex)
                    {
                        _loggerService.LogError(ex, $"创建备份文件失败: {backupFilePath}");
                    }
                }

                // 导出当前数据
                var exportedCount = await _syncService.ExportToFileAsync(syncFilePath);

                // 检查并解决冲突
                if (localConfig.EnableConflictResolution)
                {
                    await ResolveConflictsAsync(config, localConfig, syncFilePath);
                }

                // 更新最后同步时间
                config.LastSyncTime = DateTime.UtcNow;
                await _configService.SaveConfigurationAsync(config);

                _status.LastSyncTime = config.LastSyncTime;
                _status.NextSyncTime = DateTime.UtcNow.AddMinutes(localConfig.SyncIntervalMinutes);
                _status.ErrorCount = 0;
                _status.LastError = null;

                OnStatusChanged(new AutoSyncStatusEventArgs($"{reason}完成 ({exportedCount} 条记录)", false));
                _loggerService.LogInfo($"自动同步完成: {reason}, 导出 {exportedCount} 条记录");
            }
            catch (Exception ex)
            {
                _status.ErrorCount++;
                _status.LastError = ex.Message;
                _loggerService.LogError(ex, $"自动同步失败: {reason}");

                OnStatusChanged(new AutoSyncStatusEventArgs($"同步失败: {ex.Message}", false));

                // 如果错误次数过多，停止自动同步
                if (_status.ErrorCount >= 3)
                {
                    Stop();
                    OnStatusChanged(new AutoSyncStatusEventArgs("由于连续错误，自动同步已停止", false));
                }
            }
            finally
            {
                lock (_syncLock)
                {
                    _isSyncing = false;
                }
                _status.IsSyncing = false;
            }
        }

        /// <summary>
        /// 解决冲突
        /// </summary>
        private async Task ResolveConflictsAsync(SyncConfiguration config, LocalFileSyncConfig localConfig, string syncFilePath)
        {
            try
            {
                // 这里可以实现更复杂的冲突解决逻辑
                // 目前简单记录日志
                _loggerService.LogDebug($"检查冲突: {syncFilePath}, 策略: {localConfig.ConflictStrategy}");

                // 检查远程文件是否比本地新
                if (File.Exists(syncFilePath))
                {
                    var fileInfo = new FileInfo(syncFilePath);
                    if (config.LastSyncTime.HasValue && fileInfo.LastWriteTime > config.LastSyncTime.Value)
                    {
                        _loggerService.LogInfo("检测到远程文件更新，可能需要导入");
                        // 这里可以实现导入逻辑
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "解决冲突时发生错误");
            }
        }

        /// <summary>
        /// 清理旧备份文件
        /// </summary>
        private async Task CleanupOldBackupsAsync(LocalFileSyncConfig localConfig)
        {
            try
            {
                if (!Directory.Exists(localConfig.SyncFolderPath))
                    return;

                var backupPattern = "pasteList_backup_*.json";
                var backupFiles = Directory.GetFiles(localConfig.SyncFolderPath, backupPattern)
                    .OrderByDescending(f => f)
                    .Skip(localConfig.MaxBackupFiles);

                foreach (var file in backupFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _loggerService.LogDebug($"删除旧备份文件: {file}");
                    }
                    catch (Exception ex)
                    {
                        _loggerService.LogError(ex, $"删除备份文件失败: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "清理备份文件时发生错误");
            }
        }

        /// <summary>
        /// 获取本地文件配置
        /// </summary>
        private LocalFileSyncConfig GetLocalFileConfig(SyncConfiguration config)
        {
            if (string.IsNullOrEmpty(config.ConfigData))
                return new LocalFileSyncConfig();

            try
            {
                return JsonSerializer.Deserialize<LocalFileSyncConfig>(config.ConfigData) ?? new();
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "解析本地文件同步配置失败");
                return new LocalFileSyncConfig();
            }
        }

        /// <summary>
        /// 同步完成事件处理
        /// </summary>
        private void OnSyncCompleted(object? sender, SyncCompletedEventArgs e)
        {
            _loggerService.LogInfo($"同步操作完成: {e.OperationType}, 成功: {e.Success}, 记录数: {e.RecordCount}");
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        protected virtual void OnStatusChanged(AutoSyncStatusEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _syncTimer?.Dispose();
                _isDisposed = true;
                _loggerService.LogInfo("自动同步服务已释放");
            }
        }
    }
}