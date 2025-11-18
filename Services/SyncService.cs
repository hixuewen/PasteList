using PasteList.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 同步服务实现类
    /// </summary>
    public class SyncService : ISyncService
    {
        private readonly IClipboardHistoryService _clipboardHistoryService;
        private readonly ILoggerService _loggerService;

        /// <summary>
        /// 同步进度变化事件
        /// </summary>
        public event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// 同步完成事件
        /// </summary>
        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        private readonly List<SyncHistoryEntry> _syncHistory = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="clipboardHistoryService">剪贴板历史服务</param>
        /// <param name="loggerService">日志服务</param>
        public SyncService(IClipboardHistoryService clipboardHistoryService, ILoggerService loggerService)
        {
            _clipboardHistoryService = clipboardHistoryService ?? throw new ArgumentNullException(nameof(clipboardHistoryService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        /// <summary>
        /// 将剪贴板历史记录导出到文件
        /// </summary>
        public async Task<int> ExportToFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            var operationType = "导出";
            int exportedCount = 0;

            try
            {
                _loggerService.LogInfo($"开始导出剪贴板历史到文件: {filePath}");

                // 获取所有剪贴板历史记录
                var allItems = await _clipboardHistoryService.GetAllItemsAsync(int.MaxValue, 0);

                if (allItems.Count == 0)
                {
                    _loggerService.LogInfo("剪贴板历史记录为空，创建空导出文件");
                    // 创建空数组的JSON文件
                    var emptyArray = Enumerable.Empty<ClipboardItem>().ToList();
                    var json = JsonSerializer.Serialize(emptyArray, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, json, cancellationToken);

                    OnSyncCompleted(new SyncCompletedEventArgs(true, 0, null, operationType));
                    return 0;
                }

                _loggerService.LogInfo($"获取到 {allItems.Count} 条剪贴板记录");

                // 导出到JSON文件
                var jsonContent = JsonSerializer.Serialize(allItems, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(filePath, jsonContent, cancellationToken);
                exportedCount = allItems.Count;

                _loggerService.LogInfo($"成功导出 {exportedCount} 条记录到文件: {filePath}");

                // 记录同步历史
                RecordSyncHistory(operationType, exportedCount, true, null, filePath);

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(true, exportedCount, null, operationType));

                return exportedCount;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"导出剪贴板历史失败: {ex.Message}", ex);
                RecordSyncHistory(operationType, 0, false, ex.Message, filePath);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, operationType));
                throw;
            }
        }

        /// <summary>
        /// 从文件导入剪贴板历史记录
        /// </summary>
        public async Task<int> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到文件: {filePath}");

            var operationType = "导入";
            int importedCount = 0;

            try
            {
                _loggerService.LogInfo($"开始从文件导入剪贴板历史: {filePath}");

                // 读取文件内容
                var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);

                // 解析JSON
                var items = JsonSerializer.Deserialize<List<ClipboardItem>>(jsonContent);

                if (items == null || items.Count == 0)
                {
                    _loggerService.LogInfo("导入文件为空或格式错误");
                    OnSyncCompleted(new SyncCompletedEventArgs(true, 0, null, operationType));
                    return 0;
                }

                _loggerService.LogInfo($"文件包含 {items.Count} 条记录，开始导入");

                // 分批处理以避免冻结UI
                const int batchSize = 100;
                int totalProcessed = 0;

                for (int i = 0; i < items.Count; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = items.Skip(i).Take(batchSize).ToList();

                    // 检查重复并导入
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
                                importedCount++;
                            }
                            else
                            {
                                _loggerService.LogDebug($"跳过重复记录: {item.Content.Substring(0, Math.Min(50, item.Content.Length))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggerService.LogError($"导入记录失败: {ex.Message}", ex);
                        }
                    }

                    totalProcessed += batch.Count;

                    // 触发进度事件
                    var progressPercentage = (int)((double)totalProcessed / items.Count * 100);
                    OnProgressChanged(new SyncProgressEventArgs(progressPercentage, totalProcessed, items.Count, operationType));

                    // 允许UI更新
                    await Task.Delay(10, cancellationToken);
                }

                _loggerService.LogInfo($"成功导入 {importedCount} 条新记录");

                // 记录同步历史
                RecordSyncHistory(operationType, importedCount, true, null, filePath);

                // 触发完成事件
                OnSyncCompleted(new SyncCompletedEventArgs(true, importedCount, null, operationType));

                return importedCount;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                _loggerService.LogError($"导入剪贴板历史失败: {ex.Message}", ex);
                RecordSyncHistory(operationType, 0, false, ex.Message, filePath);
                OnSyncCompleted(new SyncCompletedEventArgs(false, 0, ex.Message, operationType));
                throw;
            }
        }

        /// <summary>
        /// 验证导出文件格式
        /// </summary>
        public async Task<bool> ValidateExportFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var items = JsonSerializer.Deserialize<List<ClipboardItem>>(jsonContent);
                return items != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取同步历史记录
        /// </summary>
        public Task<List<SyncHistoryEntry>> GetSyncHistoryAsync(int count = 10)
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
        private void RecordSyncHistory(string operationType, int recordCount, bool success, string? errorMessage, string? filePath)
        {
            var entry = new SyncHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                OperationType = operationType,
                RecordCount = recordCount,
                Success = success,
                ErrorMessage = errorMessage,
                FilePath = filePath
            };

            _syncHistory.Add(entry);

            // 限制历史记录数量（保留最近100条）
            if (_syncHistory.Count > 100)
            {
                _syncHistory.RemoveRange(0, _syncHistory.Count - 100);
            }

            _loggerService.LogInfo($"记录同步历史: {operationType}, 成功: {success}, 记录数: {recordCount}");
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
