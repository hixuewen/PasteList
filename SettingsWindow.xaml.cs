using System;
using System.Windows;
using System.Windows.Controls;
using PasteList.Services;
using PasteList.ViewModels;

namespace PasteList
{
    /// <summary>
    /// 设置窗口的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        /// <summary>
        /// 同步完成事件
        /// </summary>
        public event EventHandler<int>? SyncCompleted;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="authService">认证服务</param>
        /// <param name="historyService">剪贴板历史服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsWindow(IStartupService startupService, IAuthService authService, IClipboardHistoryService historyService, ILoggerService? logger)
        {
            InitializeComponent();

            // 初始化ViewModel
            _viewModel = new SettingsViewModel(startupService, authService, historyService, logger);
            DataContext = _viewModel;

            // 订阅清空密码框事件
            _viewModel.PasswordBoxClearRequested += OnPasswordBoxClearRequested;

            // 订阅同步完成事件并转发
            _viewModel.SyncCompleted += (s, count) => SyncCompleted?.Invoke(this, count);

            // 绑定窗口加载事件
            this.Loaded += Window_Loaded;
        }

        /// <summary>
        /// 窗口加载完成后的事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载当前设置
                await _viewModel.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// 处理确定按钮点击事件
        /// </summary>
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 等待异步保存操作完成
                if (_viewModel.SaveCommand is RelayCommand relayCommand)
                {
                    await relayCommand.ExecuteAsync(null);
                }
                else
                {
                    _viewModel.SaveCommand.Execute(null);
                    // 如果不是异步命令，给一个短暂延迟以确保操作完成
                    await Task.Delay(100);
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.CancelCommand.Execute(null);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理密码框密码变化事件
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.Password = passwordBox.Password;
            }
        }

        /// <summary>
        /// 处理确认密码框密码变化事件
        /// </summary>
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        /// <summary>
        /// 处理清空密码框事件
        /// </summary>
        private void OnPasswordBoxClearRequested(object? sender, EventArgs e)
        {
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }
    }
}
