using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PasteList.Services;

namespace PasteList.ViewModels
{
    /// <summary>
    /// 设置窗口的ViewModel
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IStartupService _startupService;
        private readonly ILoggerService? _logger;
        private bool _isStartupEnabled;
        private bool _hasChanges;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsViewModel(IStartupService startupService, ILoggerService? logger = null)
        {
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _logger = logger;

            // 初始化命令
            SaveCommand = new RelayCommand(
                executeAsync: SaveAsync,
                canExecute: () => HasChanges
            );

            CancelCommand = new RelayCommand(
                executeAsync: CancelAsync,
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
        /// 加载当前设置
        /// </summary>
        public Task LoadSettingsAsync()
        {
            try
            {
                // 加载开机启动设置
                _isStartupEnabled = _startupService.IsStartupEnabled();

                HasChanges = false;
                OnPropertyChanged(nameof(IsStartupEnabled));

                _logger?.LogDebug($"设置窗口加载完成，开机启动状态: {(_isStartupEnabled ? "已启用" : "已禁用")}");
                return Task.CompletedTask;
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
        private Task SaveAsync()
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

                HasChanges = false;
                _logger?.LogInfo("设置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存设置时发生错误");
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 取消设置
        /// </summary>
        private Task CancelAsync()
        {
            try
            {
                // 重新加载设置，不保存任何更改
                return LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取消设置时发生错误");
                throw;
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
