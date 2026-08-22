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

            _viewModel = new SettingsViewModel(startupService, logger);
            DataContext = _viewModel;

            this.Loaded += Window_Loaded;
        }

        /// <summary>
        /// 窗口加载完成后的事件处理
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
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
                if (_viewModel.SaveCommand is RelayCommand relayCommand)
                {
                    await relayCommand.ExecuteAsync(null);
                }
                else
                {
                    _viewModel.SaveCommand.Execute(null);
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
