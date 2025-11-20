using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 服务器同步服务实现
    /// </summary>
    public class ServerSyncService : IServerSyncService
    {
        private readonly IClipboardHistoryService _clipboardHistoryService;
        private readonly ILoggerService _loggerService;
        private readonly List<ServerSyncHistoryEntry> _syncHistory = new();

        /// <summary>
        /// 同步进度变化事件
        /// </summary>
        public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// 同步完成事件
        /// </summary>
        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="clipboardHistoryService">剪贴板历史服务</param>
        /// <param name="loggerService">日志服务</param>
        public ServerSyncService(IClipboardHistoryService clipboardHistoryService, ILoggerService loggerService)
        {
            _clipboardHistoryService = clipboardHistoryService ?? throw new ArgumentNullException(nameof(clipboardHistoryService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        /// <summary>
        /// 验证服务器连接
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(ServerSyncConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                var operationType = "验证连接";
                _loggerService.LogInfo($"开始验证服务器连接: {config.ServerUrl}");

                using var client = new ServerApiClient(config, _loggerService);
                var isValid = await client.ValidateConnectionAsync(cancellationToken);

                // 记录同步历史
                RecordSyncHistory(ServerSyncOperationType.Validate, isValid ? 1 : 0, isValid, null, config.ServerUrl, config.DeviceId);

                if (isValid)
                {
                    _loggerService.LogInfo($"服务器连接验证成功: {config.ServerUrl}");
                }
                else
                {
                    _loggerService.LogWarning($"服务器连接验证失败: {config.ServerUrl}");
                }

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(isValid, isValid ? 1 : 0, null, operationType));

                return isValid;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"验证服务器连接失败: {ex.Message}", ex);
                RecordSyncHistory(ServerSyncOperationType.Validate, 0, false, ex.Message, config.ServerUrl, config.DeviceId);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, "验证连接"));
                return false;
            }
        }

        /// <summary>
        /// 将本地剪贴板数据推送到服务器
        /// </summary>
        public async Task<int> PushAsync(ServerSyncConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var operationType = "推送";
            int pushedCount = 0;

            try
            {
                _loggerService.LogInfo($"开始推送剪贴板数据到服务器: {config.ServerUrl}");

                // 获取所有本地剪贴板记录
                var localItems = await _clipboardHistoryService.GetAllItemsAsync(int.MaxValue, 0);

                if (localItems.Count == 0)
                {
                    _loggerService.LogInfo("本地剪贴板历史记录为空，无需推送");
                    OnSyncCompleted(new SyncCompletedEventArgs(true, 0, null, operationType));
                    return 0;
                }

                _loggerService.LogInfo($"获取到 {localItems.Count} 条本地剪贴板记录");

                // 使用HTTP客户端推送数据
                using var client = new ServerApiClient(config, _loggerService);
                var result = await client.PushItemsAsync(localItems, config.DeviceId, cancellationToken);

                if (!result.Success)
                {
                    var errorMessage = result.ErrorMessage ?? "推送失败";
                    _loggerService.LogError(errorMessage);
                    RecordSyncHistory(ServerSyncOperationType.Push, 0, false, errorMessage, config.ServerUrl, config.DeviceId);
                    OnSyncCompleted(new SyncCompletedEventArgs(false, 0, errorMessage, operationType));
                    throw new Exception(errorMessage);
                }

                pushedCount = result.PushedCount;

                // 记录同步历史
                RecordSyncHistory(ServerSyncOperationType.Push, pushedCount, true, null, config.ServerUrl, config.DeviceId);

                _loggerService.LogInfo($"成功推送 {pushedCount} 条记录到服务器");

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(true, pushedCount, null, operationType));

                return pushedCount;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"推送数据到服务器失败: {ex.Message}", ex);
                RecordSyncHistory(ServerSyncOperationType.Push, 0, false, ex.Message, config.ServerUrl, config.DeviceId);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, operationType));
                throw;
            }
        }

        /// <summary>
        /// 从服务器拉取剪贴板数据
        /// </summary>
        public async Task<int> PullAsync(ServerSyncConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var operationType = "拉取";
            int pulledCount = 0;

            try
            {
                _loggerService.LogInfo($"开始从服务器拉取剪贴板数据: {config.ServerUrl}");

                using var client = new ServerApiClient(config, _loggerService);
                var serverItems = await client.PullItemsAsync(config.DeviceId, config.LastSyncTime, cancellationToken);

                if (serverItems.Count == 0)
                {
                    _loggerService.LogInfo("服务器端没有新数据");
                    OnSyncCompleted(new SyncCompletedEventArgs(true, 0, null, operationType));
                    return 0;
                }

                _loggerService.LogInfo($"服务器返回 {serverItems.Count} 条记录，开始导入本地数据库");

                // 分批处理以避免冻结UI
                const int batchSize = 100;
                int totalProcessed = 0;

                for (int i = 0; i < serverItems.Count; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = serverItems.Skip(i).Take(batchSize).ToList();

                    foreach (var item in batch)
                    {
                        try
                        {
                            // 检查是否已存在相同内容
                            var existingItem = await _clipboardHistoryService.FindDuplicateAsync(item.Content);
                            if (existingItem == null)
                            {
                                // 不存在重复，添加到数据库
                                await _clipboardHistoryService.AddItemAsync(item);
                                pulledCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggerService.LogError($"导入记录失败: {ex.Message}", ex);
                        }
                    }

                    totalProcessed += batch.Count;

                    // 触发进度事件
                    var progressPercentage = (int)((double)totalProcessed / serverItems.Count * 100);
                    OnProgressChanged(new SyncProgressEventArgs(progressPercentage, totalProcessed, serverItems.Count, operationType));

                    // 允许UI更新
                    await Task.Delay(10, cancellationToken);
                }

                // 更新最后同步时间
                config.LastSyncTime = DateTime.UtcNow;

                // 记录同步历史
                RecordSyncHistory(ServerSyncOperationType.Pull, pulledCount, true, null, config.ServerUrl, config.DeviceId);

                _loggerService.LogInfo($"成功从服务器拉取并导入 {pulledCount} 条记录");

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(true, pulledCount, null, operationType));

                return pulledCount;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"从服务器拉取数据失败: {ex.Message}", ex);
                RecordSyncHistory(ServerSyncOperationType.Pull, 0, false, ex.Message, config.ServerUrl, config.DeviceId);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, operationType));
                throw;
            }
        }

        /// <summary>
        /// 双向同步（在本地和服务器之间同步数据）
        /// </summary>
        public async Task<SyncResult> BidirectionalSyncAsync(ServerSyncConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var operationType = "双向同步";
            var result = new SyncResult();

            try
            {
                _loggerService.LogInfo($"开始双向同步: {config.ServerUrl}");

                // 获取所有本地剪贴板记录
                var localItems = await _clipboardHistoryService.GetAllItemsAsync(int.MaxValue, 0);
                _loggerService.LogDebug($"本地共有 {localItems.Count} 条记录");

                using var client = new ServerApiClient(config, _loggerService);
                var syncResult = await client.BidirectionalSyncAsync(localItems, config.DeviceId, config.LastSyncTime, cancellationToken);

                if (!syncResult.Success)
                {
                    var errorMessage = syncResult.ErrorMessage ?? "双向同步失败";
                    _loggerService.LogError(errorMessage);
                    result = new SyncResult(errorMessage);
                    RecordSyncHistory(ServerSyncOperationType.Bidirectional, 0, false, errorMessage, config.ServerUrl, config.DeviceId);
                    OnSyncCompleted(new SyncCompletedEventArgs(false, 0, errorMessage, operationType));
                    return result;
                }

                result.PushedCount = syncResult.PushedCount;
                result.PulledCount = syncResult.PulledCount;
                result.ConflictsResolved = syncResult.ConflictsResolved;
                result.Success = true;
                result.LastSyncTime = DateTime.UtcNow;

                // 处理从服务器返回的数据
                if (syncResult.ServerItems.Count > 0)
                {
                    _loggerService.LogInfo($"处理服务器返回的 {syncResult.ServerItems.Count} 条记录");

                    int importedCount = 0;
                    foreach (var item in syncResult.ServerItems)
                    {
                        try
                        {
                            var existingItem = await _clipboardHistoryService.FindDuplicateAsync(item.Content);
                            if (existingItem == null)
                            {
                                await _clipboardHistoryService.AddItemAsync(item);
                                importedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggerService.LogError($"导入服务器记录失败: {ex.Message}", ex);
                        }
                    }

                    result.PulledCount = importedCount;
                    _loggerService.LogInfo($"从服务器导入 {importedCount} 条新记录");
                }

                // 更新最后同步时间
                config.LastSyncTime = result.LastSyncTime;

                // 记录同步历史
                RecordSyncHistory(ServerSyncOperationType.Bidirectional, result.TotalRecords, true, null,
                    config.ServerUrl, config.DeviceId, result.PushedCount, result.PulledCount, result.ConflictsResolved);

                _loggerService.LogInfo($"双向同步完成：推送 {result.PushedCount} 条，拉取 {result.PulledCount} 条，解决冲突 {result.ConflictsResolved} 个");

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(true, result.TotalRecords, null, operationType));

                return result;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"双向同步失败: {ex.Message}", ex);
                result = new SyncResult($"双向同步失败: {ex.Message}");
                RecordSyncHistory(ServerSyncOperationType.Bidirectional, 0, false, ex.Message, config.ServerUrl, config.DeviceId);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, operationType));
                return result;
            }
        }

        /// <summary>
        /// 获取同步历史记录
        /// </summary>
        public Task<List<ServerSyncHistoryEntry>> GetSyncHistoryAsync(int count = 10)
        {
            var result = _syncHistory
                .OrderByDescending(h => h.Timestamp)
                .Take(count)
                .ToList();
            return Task.FromResult(result);
        }

        /// <summary>
        /// 清理同步历史
        /// </summary>
        public Task<int> CleanSyncHistoryAsync(DateTime beforeDate)
        {
            var countToRemove = _syncHistory.Count(h => h.Timestamp < beforeDate);
            _syncHistory.RemoveAll(h => h.Timestamp < beforeDate);
            return Task.FromResult(countToRemove);
        }

        /// <summary>
        /// 记录同步历史
        /// </summary>
        private void RecordSyncHistory(ServerSyncOperationType operationType, int recordCount, bool success,
            string? errorMessage, string? serverUrl, string? deviceId,
            int? pushedCount = null, int? pulledCount = null, int? conflictsResolved = null)
        {
            var entry = new ServerSyncHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                OperationType = operationType,
                RecordCount = recordCount,
                PushedCount = pushedCount,
                PulledCount = pulledCount,
                ConflictsResolved = conflictsResolved,
                Success = success,
                ErrorMessage = errorMessage,
                ServerUrl = serverUrl,
                DeviceId = deviceId
            };

            _syncHistory.Add(entry);

            // 限制历史记录数量（最多保存100条）
            if (_syncHistory.Count > 100)
            {
                _syncHistory.RemoveRange(0, _syncHistory.Count - 100);
            }
        }

        /// <summary>
        /// 触发进度变化事件
        /// </summary>
        protected virtual void OnProgressChanged(SyncProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发同步完成事件
        /// </summary>
        protected virtual void OnSyncCompleted(SyncCompletedEventArgs e)
        {
            SyncCompleted?.Invoke(this, e);
        }
    }
}
