using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using PasteList.Models;
using PasteList.Services;

// 为解决命名空间冲突，添加别名
using WpfMessageBox = System.Windows.MessageBox;

namespace PasteList.ViewModels
{
    /// <summary>
    /// 设置窗口的ViewModel
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IStartupService _startupService;
        private readonly ISyncConfigurationService _syncConfigurationService;
        private readonly ILoggerService? _logger;
        private bool _isStartupEnabled;
        private bool _isSyncEnabled;
        private string _syncType = "LocalFile";
        private SyncConfiguration? _syncConfiguration;
        private LocalFileSyncConfig _localFileSyncConfig = new();
        private ServerSyncConfig _serverSyncConfig = new();
        private bool _hasChanges;
        private string _syncStatusText = "未配置";
        private string _lastSyncTimeText = "从未同步";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="syncConfigurationService">同步配置服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsViewModel(IStartupService startupService, ISyncConfigurationService syncConfigurationService, ILoggerService? logger = null)
        {
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _syncConfigurationService = syncConfigurationService ?? throw new ArgumentNullException(nameof(syncConfigurationService));
            _logger = logger;

            // 初始化命令
            SaveCommand = new RelayCommand(
                execute: async () => await SaveAsync(),
                canExecute: () => HasChanges
            );

            CancelCommand = new RelayCommand(
                execute: async () => await CancelAsync(),
                canExecute: () => true
            );

            BrowseFolderCommand = new RelayCommand(
                execute: BrowseFolder,
                canExecute: () => true
            );
        }

        /// <summary>
        /// 是否启用开机启动
        /// </summary>
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (_isStartupEnabled != value)
                {
                    _isStartupEnabled = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否启用同步
        /// </summary>
        public bool IsSyncEnabled
        {
            get => _isSyncEnabled;
            set
            {
                if (_isSyncEnabled != value)
                {
                    _isSyncEnabled = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 同步类型
        /// </summary>
        public string SyncType
        {
            get => _syncType;
            set
            {
                if (_syncType != value)
                {
                    _syncType = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLocalFileSyncType));
                    OnPropertyChanged(nameof(IsServerSyncType));
                    UpdateSyncStatusText();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否为 LocalFile 同步类型
        /// </summary>
        public bool IsLocalFileSyncType => SyncType == "LocalFile";

        /// <summary>
        /// 是否为 Server 同步类型
        /// </summary>
        public bool IsServerSyncType => SyncType == "Server";

        /// <summary>
        /// 本地文件同步配置
        /// </summary>
        public LocalFileSyncConfig LocalFileSyncConfig
        {
            get => _localFileSyncConfig;
            set
            {
                if (_localFileSyncConfig != value)
                {
                    _localFileSyncConfig = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    UpdateSyncStatusText();
                }
            }
        }

        /// <summary>
        /// 服务器同步配置
        /// </summary>
        public ServerSyncConfig ServerSyncConfig
        {
            get => _serverSyncConfig;
            set
            {
                if (_serverSyncConfig != value)
                {
                    _serverSyncConfig = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    UpdateSyncStatusText();
                }
            }
        }

        /// <summary>
        /// 同步状态文本
        /// </summary>
        public string SyncStatusText
        {
            get => _syncStatusText;
            private set
            {
                if (_syncStatusText != value)
                {
                    _syncStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 最后同步时间文本
        /// </summary>
        public string LastSyncTimeText
        {
            get => _lastSyncTimeText;
            private set
            {
                if (_lastSyncTimeText != value)
                {
                    _lastSyncTimeText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否有未保存的更改
        /// </summary>
        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                _hasChanges = value;
                OnPropertyChanged();
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// 浏览文件夹命令
        /// </summary>
        public ICommand BrowseFolderCommand { get; }

        /// <summary>
        /// 加载当前设置
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                // 加载开机启动设置
                _isStartupEnabled = _startupService.IsStartupEnabled();

                // 加载同步配置
                try
                {
                    _syncConfiguration = await _syncConfigurationService.GetCurrentConfigurationAsync();
                    _isSyncEnabled = _syncConfiguration.IsEnabled;
                    _syncType = _syncConfiguration.SyncType;

                    // 加载 LocalFile 特定配置
                    if (_syncConfiguration.SyncType == "LocalFile" && !string.IsNullOrEmpty(_syncConfiguration.ConfigData))
                    {
                        try
                        {
                            _localFileSyncConfig = JsonSerializer.Deserialize<LocalFileSyncConfig>(_syncConfiguration.ConfigData) ?? new();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "解析 LocalFile 同步配置失败");
                            _localFileSyncConfig = new();
                        }
                    }
                    else
                    {
                        _localFileSyncConfig = new();
                    }

                    // 加载 Server 特定配置
                    if (_syncConfiguration.SyncType == "Server" && !string.IsNullOrEmpty(_syncConfiguration.ConfigData))
                    {
                        try
                        {
                            _serverSyncConfig = JsonSerializer.Deserialize<ServerSyncConfig>(_syncConfiguration.ConfigData) ?? new();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "解析 Server 同步配置失败");
                            _serverSyncConfig = new();
                        }
                    }
                    else
                    {
                        _serverSyncConfig = new();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // 处理数据库表不存在的情况
                    _logger?.LogError(ex, "同步配置加载失败，数据库表可能不存在");
                    _isSyncEnabled = false;
                    _syncType = "LocalFile";
                    _syncConfiguration = new SyncConfiguration(_syncType, _isSyncEnabled);
                    _localFileSyncConfig = new();
                }

                // 更新同步状态文本
                UpdateSyncStatusText();

                HasChanges = false;
                OnPropertyChanged(nameof(IsStartupEnabled));
                OnPropertyChanged(nameof(IsSyncEnabled));
                OnPropertyChanged(nameof(SyncType));
                OnPropertyChanged(nameof(IsLocalFileSyncType));
                OnPropertyChanged(nameof(IsServerSyncType));
                OnPropertyChanged(nameof(LocalFileSyncConfig));
                OnPropertyChanged(nameof(ServerSyncConfig));

                _logger?.LogDebug($"设置窗口加载完成，开机启动状态: {(_isStartupEnabled ? "已启用" : "已禁用")}，同步状态: {(_isSyncEnabled ? "已启用" : "已禁用")}，同步类型: {_syncType}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载设置时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private async Task SaveAsync()
        {
            try
            {
                // 保存开机启动设置
                var oldStartupState = _startupService.IsStartupEnabled();

                if (IsStartupEnabled && !oldStartupState)
                {
                    _startupService.EnableStartup();
                    _logger?.LogUserAction("启用开机启动", "通过设置窗口");
                }
                else if (!IsStartupEnabled && oldStartupState)
                {
                    _startupService.DisableStartup();
                    _logger?.LogUserAction("禁用开机启动", "通过设置窗口");
                }

                // 保存同步配置
                if (_syncConfiguration != null)
                {
                    try
                    {
                        // 更新配置对象
                        _syncConfiguration.IsEnabled = IsSyncEnabled;
                        _syncConfiguration.SyncType = SyncType;
                        _syncConfiguration.UpdatedAt = DateTime.UtcNow;

                        // 保存 LocalFile 特定配置
                        if (SyncType == "LocalFile")
                        {
                            // 验证 LocalFile 配置
                            var (isValid, errorMessage) = _localFileSyncConfig.Validate();
                            if (!isValid)
                            {
                                throw new InvalidOperationException($"LocalFile 同步配置无效: {errorMessage}");
                            }

                            _syncConfiguration.ConfigData = JsonSerializer.Serialize(_localFileSyncConfig, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                        }
                        else if (SyncType == "Server")
                        {
                            // 验证 Server 配置
                            var (isValid, errorMessage) = _serverSyncConfig.Validate();
                            if (!isValid)
                            {
                                throw new InvalidOperationException($"Server 同步配置无效: {errorMessage}");
                            }

                            _syncConfiguration.ConfigData = JsonSerializer.Serialize(_serverSyncConfig, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                        }
                        else
                        {
                            _syncConfiguration.ConfigData = null;
                        }

                        var saveResult = await _syncConfigurationService.SaveConfigurationAsync(_syncConfiguration);
                        if (saveResult)
                        {
                            _logger?.LogUserAction($"保存同步配置: 启用={IsSyncEnabled}, 类型={SyncType}, 文件夹={_localFileSyncConfig.SyncFolderPath}", "通过设置窗口");
                        }
                        else
                        {
                            _logger?.LogError("同步配置保存失败");
                            throw new InvalidOperationException("同步配置保存失败，请检查数据库连接");
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("数据库表不存在"))
                    {
                        _logger?.LogError(ex, "保存同步配置失败：数据库表不存在");
                        throw new InvalidOperationException("无法保存同步配置，因为数据库表不存在。请重新启动应用程序以自动创建必要的数据库表。", ex);
                    }
                }

                HasChanges = false;
                _logger?.LogInfo("设置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存设置时发生错误");
                WpfMessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消设置
        /// </summary>
        private async Task CancelAsync()
        {
            try
            {
                // 重新加载设置，不保存任何更改
                await LoadSettingsAsync();
                _logger?.LogDebug("用户取消设置，已丢弃更改");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取消设置时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            try
            {
                using var dialog = new FolderBrowserDialog();
                dialog.Description = "选择同步文件夹";
                dialog.SelectedPath = LocalFileSyncConfig.SyncFolderPath;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // 验证文件夹
                    if (ValidateSyncFolder(dialog.SelectedPath))
                    {
                        LocalFileSyncConfig.SyncFolderPath = dialog.SelectedPath;
                        OnPropertyChanged(nameof(LocalFileSyncConfig));
                        _logger?.LogUserAction($"选择同步文件夹: {dialog.SelectedPath}", "通过设置窗口");
                    }
                    else
                    {
                        WpfMessageBox.Show("选择的文件夹不可用或权限不足", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "浏览文件夹时发生错误");
                WpfMessageBox.Show($"浏览文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 验证同步文件夹
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>是否有效</returns>
        private bool ValidateSyncFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return false;

            try
            {
                // 检查文件夹是否存在
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 测试写入权限
                var testFile = Path.Combine(folderPath, ".pasteList_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"验证文件夹失败: {folderPath}");
                return false;
            }
        }

        /// <summary>
        /// 更新同步状态文本
        /// </summary>
        private void UpdateSyncStatusText()
        {
            if (!IsSyncEnabled)
            {
                SyncStatusText = "未启用";
                LastSyncTimeText = "从未同步";
                return;
            }

            if (SyncType == "LocalFile")
            {
                if (string.IsNullOrWhiteSpace(LocalFileSyncConfig.SyncFolderPath))
                {
                    SyncStatusText = "未配置文件夹";
                    LastSyncTimeText = "从未同步";
                    return;
                }

                if (!Directory.Exists(LocalFileSyncConfig.SyncFolderPath))
                {
                    SyncStatusText = "文件夹不存在";
                    LastSyncTimeText = "从未同步";
                    return;
                }

                SyncStatusText = "配置正常";
            }
            else if (SyncType == "Server")
            {
                var (isValid, errorMessage) = ServerSyncConfig.Validate();
                if (!isValid)
                {
                    SyncStatusText = $"配置无效: {errorMessage}";
                    LastSyncTimeText = "从未同步";
                    return;
                }

                SyncStatusText = "配置正常";
            }
            else
            {
                SyncStatusText = "未配置";
                LastSyncTimeText = "从未同步";
                return;
            }

            if (_syncConfiguration?.LastSyncTime.HasValue == true)
            {
                LastSyncTimeText = $"上次同步: {_syncConfiguration.LastSyncTime:yyyy-MM-dd HH:mm:ss}";
            }
            else
            {
                LastSyncTimeText = "从未同步";
            }
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
