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

            // 窗口关闭事件处理
            this.Closing += SettingsWindow_Closing;
        }

        /// <summary>
        /// 窗口加载完成后的事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载当前设置
                _viewModel.LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果有关闭按钮事件处理，会在这里处理
        }
    }
}
