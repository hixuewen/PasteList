using System;
using System.Windows;
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
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsWindow(IStartupService startupService, ILoggerService? logger)
        {
            InitializeComponent();

            // 初始化ViewModel
            _viewModel = new SettingsViewModel(startupService, logger);
            DataContext = _viewModel;

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
    }
}
