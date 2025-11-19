using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PasteList.Models;
using PasteList.Services;

namespace PasteList.ViewModels
{
    /// <summary>
    /// 同步状态窗口的ViewModel
    /// </summary>
    public class SyncStatusViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISyncService _syncService;
        private readonly ISyncConfigurationService _configService;
        private readonly IAutoSyncService _autoSyncService;
        private readonly ILoggerService? _loggerService;

        private string _currentStatus = "未知";
        private ObservableCollection<SyncHistoryEntry> _syncHistory = new();
        private bool _isRefreshing = false;
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SyncStatusViewModel(
            ISyncService syncService,
            ISyncConfigurationService configService,
            IAutoSyncService autoSyncService,
            ILoggerService? loggerService = null)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _autoSyncService = autoSyncService ?? throw new ArgumentNullException(nameof(autoSyncService));
            _loggerService = loggerService;

            // 初始化命令 - 同时检查刷新状态和自动同步状态
            SyncNowCommand = new RelayCommand(
                async () => await SyncNowAsync(),
                () => !_isRefreshing && !_autoSyncService.GetCurrentStatus().IsSyncing);
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !_isRefreshing);
            ClearHistoryCommand = new RelayCommand(async () => await ClearHistoryAsync(), () => _syncHistory.Count > 0);

            // 订阅事件
            _autoSyncService.StatusChanged += OnAutoSyncStatusChanged;
            _syncService.SyncCompleted += OnSyncCompleted;
        }

        #region 属性

        /// <summary>
        /// 当前同步状态
        /// </summary>
        public string CurrentStatus
        {
            get => _currentStatus;
            private set
            {
                if (_currentStatus != value)
                {
                    _currentStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 同步历史记录
        /// </summary>
        public ObservableCollection<SyncHistoryEntry> SyncHistory
        {
            get => _syncHistory;
            private set
            {
                if (_syncHistory != value)
                {
                    _syncHistory = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 同步次数
        /// </summary>
        public int SyncCount => _syncHistory.Count;

        /// <summary>
        /// 成功次数
        /// </summary>
        public int SuccessCount => _syncHistory.Count(h => h.Success);

        /// <summary>
        /// 失败次数
        /// </summary>
        public int ErrorCount => _syncHistory.Count(h => !h.Success);

        /// <summary>
        /// 同步文件夹
        /// </summary>
        public string SyncFolder { get; private set; } = "未配置";

        /// <summary>
        /// 最后同步时间文本
        /// </summary>
        public string LastSyncTimeText { get; private set; } = "从未同步";

        /// <summary>
        /// 同步类型文本
        /// </summary>
        public string SyncTypeText { get; private set; } = "未知";

        /// <summary>
        /// 同步间隔文本
        /// </summary>
        public string SyncIntervalText { get; private set; } = "未知";

        #endregion

        #region 命令

        /// <summary>
        /// 立即同步命令
        /// </summary>
        public ICommand SyncNowCommand { get; }

        /// <summary>
        /// 刷新命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 清空历史命令
        /// </summary>
        public ICommand ClearHistoryCommand { get; }

        #endregion

        #region 方法

        /// <summary>
        /// 初始化
        /// </summary>
        internal async Task InitializeAsync()
        {
            await RefreshAsync();
            UpdateSyncStatus();
        }

        /// <summary>
        /// 立即同步
        /// </summary>
        private async Task SyncNowAsync()
        {
            _loggerService?.LogDebug("开始执行手动同步...");

            // 检查自动同步是否正在进行
            var currentStatus = _autoSyncService.GetCurrentStatus();

            if (currentStatus.IsSyncing)
            {
                _loggerService?.LogDebug("手动同步被忽略：自动同步正在进行");
                CurrentStatus = "自动同步进行中，无法手动同步";
                return;
            }

            if (_isRefreshing)
            {
                _loggerService?.LogDebug("手动同步被忽略：正在刷新数据");
                return;
            }

            _isRefreshing = true;
            // 通知命令状态已更新，使按钮正确显示为禁用状态
            ((RelayCommand)SyncNowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();

            try
            {
                var config = await _configService.GetCurrentConfigurationAsync();
                if (config?.IsEnabled != true)
                {
                    _loggerService?.LogDebug("同步被忽略：同步功能未启用");
                    CurrentStatus = "同步功能未启用";
                    return;
                }

                CurrentStatus = "正在手动同步...";
                _loggerService?.LogInfo("开始执行手动同步操作");

                await _autoSyncService.ManualSyncAsync("手动同步");

                _loggerService?.LogUserAction("执行手动同步", "通过同步状态窗口");
                _loggerService?.LogDebug("手动同步操作完成");
            }
            catch (Exception ex)
            {
                var errorMessage = $"同步失败: {ex.Message}";
                CurrentStatus = errorMessage;
                _loggerService?.LogError(ex, "手动同步失败");

                // 由于 RelayCommand 在UI线程执行，可以直接显示 MessageBox
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isRefreshing = false;
                // 通知命令状态已更新，使按钮正确显示为启用状态
                ((RelayCommand)SyncNowCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 刷新状态
        /// </summary>
        private async Task RefreshAsync()
        {
            try
            {
                // 检查自动同步是否正在进行
                if (_autoSyncService.GetCurrentStatus().IsSyncing)
                {
                    _loggerService?.LogDebug("刷新被延迟：自动同步正在进行");
                    // 等待自动同步完成
                    while (_autoSyncService.GetCurrentStatus().IsSyncing)
                    {
                        await Task.Delay(100);
                    }
                }

                if (_isRefreshing) return;

                _isRefreshing = true;
                // 通知命令状态已更新，使按钮正确显示为禁用状态
                ((RelayCommand)SyncNowCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();

                try
                {
                    // 加载同步历史
                    var history = await _syncService.GetSyncHistoryAsync(100);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        SyncHistory.Clear();
                        foreach (var entry in history)
                        {
                            SyncHistory.Add(entry);
                        }
                    });

                    // 更新统计信息
                    OnPropertyChanged(nameof(SyncCount));
                    OnPropertyChanged(nameof(SuccessCount));
                    OnPropertyChanged(nameof(ErrorCount));

                    // 更新配置信息
                    await UpdateConfigurationInfoAsync();

                    // 更新当前状态
                    UpdateSyncStatus();

                    _loggerService?.LogDebug("同步状态已刷新");
                }
                catch (Exception ex)
                {
                    var errorMessage = $"刷新失败: {ex.Message}";
                    _loggerService?.LogError(ex, "刷新同步状态失败");

                    // 由于 RelayCommand 在UI线程执行，可以直接显示 MessageBox
                    MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isRefreshing = false;
                // 通知命令状态已更新，使按钮正确显示为启用状态
                ((RelayCommand)SyncNowCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        private async Task ClearHistoryAsync()
        {
            try
            {
                // 确保在 UI 线程上显示确认对话框
                var result = await App.Current?.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("确定要清空所有同步历史记录吗？", "确认",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                }).Task;

                if (result == MessageBoxResult.Yes)
                {
                    await _syncService.CleanSyncHistoryAsync(DateTime.UtcNow.AddYears(100));
                    await RefreshAsync();
                    _loggerService?.LogUserAction("清空同步历史记录", "通过同步状态窗口");
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"清空失败: {ex.Message}";
                _loggerService?.LogError(ex, "清空同步历史失败");

                // 由于 RelayCommand 在UI线程执行，可以直接显示 MessageBox
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新配置信息
        /// </summary>
        private async Task UpdateConfigurationInfoAsync()
        {
            try
            {
                var config = await _configService.GetCurrentConfigurationAsync();

                // 更新同步类型
                SyncTypeText = config?.SyncType ?? "未知";

                // 更新同步间隔
                if (config?.SyncType == "LocalFile" && !string.IsNullOrEmpty(config.ConfigData))
                {
                    try
                    {
                        var localConfig = System.Text.Json.JsonSerializer.Deserialize<LocalFileSyncConfig>(config.ConfigData);
                        if (localConfig != null)
                        {
                            SyncIntervalText = $"{localConfig.SyncIntervalMinutes} 分钟";
                            SyncFolder = localConfig.SyncFolderPath;
                        }
                    }
                    catch
                    {
                        SyncIntervalText = "配置解析失败";
                        SyncFolder = "配置解析失败";
                    }
                }
                else
                {
                    SyncIntervalText = "不适用";
                    SyncFolder = "不适用";
                }

                // 更新最后同步时间
                if (config?.LastSyncTime.HasValue == true)
                {
                    LastSyncTimeText = $"上次同步: {config.LastSyncTime:yyyy-MM-dd HH:mm:ss}";
                }
                else
                {
                    LastSyncTimeText = "从未同步";
                }

                OnPropertyChanged(nameof(SyncFolder));
                OnPropertyChanged(nameof(LastSyncTimeText));
                OnPropertyChanged(nameof(SyncTypeText));
                OnPropertyChanged(nameof(SyncIntervalText));
            }
            catch (Exception ex)
            {
                _loggerService?.LogError(ex, "更新配置信息失败");
            }
        }

        /// <summary>
        /// 更新同步状态
        /// </summary>
        private void UpdateSyncStatus()
        {
            try
            {
                var autoStatus = _autoSyncService.GetCurrentStatus();

                if (autoStatus.IsRunning)
                {
                    if (autoStatus.IsSyncing)
                    {
                        CurrentStatus = "正在同步...";
                    }
                    else
                    {
                        CurrentStatus = "运行中";
                    }
                }
                else
                {
                    CurrentStatus = "已停止";
                }

                if (!string.IsNullOrEmpty(autoStatus.LastError))
                {
                    CurrentStatus += $" (错误: {autoStatus.LastError})";
                }
            }
            catch (Exception ex)
            {
                _loggerService?.LogError(ex, "更新同步状态失败");
                CurrentStatus = "状态获取失败";
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 自动同步状态变化事件处理
        /// </summary>
        private void OnAutoSyncStatusChanged(object? sender, AutoSyncStatusEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                CurrentStatus = e.Message;

                // 根据同步状态更新命令的可用性
                if (SyncNowCommand is RelayCommand syncCmd)
                {
                    syncCmd.RaiseCanExecuteChanged();
                }
                if (RefreshCommand is RelayCommand refreshCmd)
                {
                    refreshCmd.RaiseCanExecuteChanged();
                }
            });
        }

        /// <summary>
        /// 同步完成事件处理
        /// </summary>
        private async void OnSyncCompleted(object? sender, SyncCompletedEventArgs e)
        {
            // 如果当前正在刷新状态，避免竞态条件，延迟刷新
            if (_isRefreshing)
            {
                // 延迟500ms后再次检查，如果仍在刷新则等待完成
                await Task.Delay(500);
                if (_isRefreshing) return; // 如果仍在刷新，跳过此次刷新
            }

            await RefreshAsync();
        }

        #endregion

        #region INotifyPropertyChanged

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _autoSyncService.StatusChanged -= OnAutoSyncStatusChanged;
                _syncService.SyncCompleted -= OnSyncCompleted;
                _disposed = true;
            }
        }

        #endregion
    }
}