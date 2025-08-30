using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PasteList.ViewModels;
using PasteList.Services;
using PasteList.Data;

namespace PasteList
{
    /// <summary>
    /// MainWindow的交互逻辑
    /// 负责初始化ViewModel和服务，建立MVVM架构
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IClipboardService _clipboardService;
        private readonly IClipboardHistoryService _historyService;
        private readonly ClipboardDbContext _dbContext;

        /// <summary>
        /// 初始化MainWindow，设置数据上下文和服务
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化数据库上下文
            _dbContext = new ClipboardDbContext();
            
            // 确保数据库已创建
            _dbContext.Database.EnsureCreated();
            
            // 初始化服务
            _clipboardService = new ClipboardService(this);
            _historyService = new ClipboardHistoryService(_dbContext);
            
            // 初始化ViewModel
            _viewModel = new MainWindowViewModel(_clipboardService, _historyService);
            
            // 设置数据上下文
            DataContext = _viewModel;
            
            // 订阅窗口关闭事件
            Closing += MainWindow_Closing;
            
            // 窗口加载完成后初始化ViewModel
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// 初始化ViewModel的数据加载
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载历史记录数据
                await _viewModel.LoadHistoryAsync();
                
                // 设置初始状态消息
                _viewModel.StatusMessage = "应用程序已就绪";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// 清理资源，停止服务
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 停止剪贴板监听
                _clipboardService?.StopListening();
                
                // 释放ViewModel资源
                _viewModel?.Dispose();
                
                // 释放历史服务资源
                _historyService?.Dispose();
                
                // 释放数据库上下文资源
                _dbContext?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止窗口关闭
                System.Diagnostics.Debug.WriteLine($"关闭窗口时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理ListView双击事件
        /// 调用ViewModel中的双击命令
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 确保有选中项且命令可执行
            if (_viewModel.SelectedItem != null && _viewModel.DoubleClickItemCommand.CanExecute(null))
            {
                _viewModel.DoubleClickItemCommand.Execute(null);
            }
        }

        /// <summary>
        /// 处理未捕获的异常
        /// 显示错误消息给用户
        /// </summary>
        /// <param name="ex">异常对象</param>
        private void HandleException(Exception ex)
        {
            string errorMessage = $"发生错误: {ex.Message}";
            
            // 更新状态消息
            if (_viewModel != null)
            {
                _viewModel.StatusMessage = errorMessage;
            }
            
            // 显示错误对话框
            MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // 记录到调试输出
            System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");
        }
    }
}