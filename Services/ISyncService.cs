using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 同步服务接口
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// 同步进度变化事件
        /// </summary>
        event EventHandler<SyncProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// 同步完成事件
        /// </summary>
        event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// 将剪贴板历史记录导出到文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导出的记录数量</returns>
        Task<int> ExportToFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从文件导入剪贴板历史记录
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入的记录数量</returns>
        Task<int> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证导出文件格式
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否有效</returns>
        Task<bool> ValidateExportFileAsync(string filePath);

        /// <summary>
        /// 获取同步历史记录
        /// </summary>
        /// <param name="count">获取数量</param>
        /// <returns>同步历史列表</returns>
        Task<List<SyncHistoryEntry>> GetSyncHistoryAsync(int count = 10);

        /// <summary>
        /// 清理同步历史
        /// </summary>
        /// <param name="beforeDate">清理此日期之前的记录</param>
        /// <returns>清理的记录数量</returns>
        Task<int> CleanSyncHistoryAsync(DateTime beforeDate);
    }

    /// <summary>
    /// 同步进度事件参数
    /// </summary>
    public class SyncProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 当前进度百分比
        /// </summary>
        public int ProgressPercentage { get; }

        /// <summary>
        /// 当前处理的记录数量
        /// </summary>
        public int CurrentCount { get; }

        /// <summary>
        /// 总记录数量
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// 操作类型（导出/导入）
        /// </summary>
        public string OperationType { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="progressPercentage">进度百分比</param>
        /// <param name="currentCount">当前记录数</param>
        /// <param name="totalCount">总记录数</param>
        /// <param name="operationType">操作类型</param>
        public SyncProgressEventArgs(int progressPercentage, int currentCount, int totalCount, string operationType)
        {
            ProgressPercentage = progressPercentage;
            CurrentCount = currentCount;
            TotalCount = totalCount;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// 同步完成事件参数
    /// </summary>
    public class SyncCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 处理记录数量
        /// </summary>
        public int RecordCount { get; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public string OperationType { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="recordCount">记录数量</param>
        /// <param name="errorMessage">错误信息</param>
        /// <param name="operationType">操作类型</param>
        public SyncCompletedEventArgs(bool success, int recordCount, string? errorMessage, string operationType)
        {
            Success = success;
            RecordCount = recordCount;
            ErrorMessage = errorMessage;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// 同步历史记录条目
    /// </summary>
    public class SyncHistoryEntry
    {
        /// <summary>
        /// 操作时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 操作类型（导出/导入）
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// 记录数量
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string? FilePath { get; set; }
    }
}
