using System.Windows;
using PasteList.Services;
using PasteList.ViewModels;

namespace PasteList.Views
{
    /// <summary>
    /// SyncStatusWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SyncStatusWindow : Window
    {
        private readonly SyncStatusViewModel _viewModel;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SyncStatusWindow(
            ISyncService syncService,
            ISyncConfigurationService configService,
            IAutoSyncService autoSyncService,
            ILoggerService? loggerService = null)
        {
            InitializeComponent();

            // 创建 ViewModel
            _viewModel = new SyncStatusViewModel(syncService, configService, autoSyncService, loggerService);
            DataContext = _viewModel;

            // 订阅窗口关闭事件
            Closing += SyncStatusWindow_Closing;

            // 窗口加载完成后异步初始化数据
            Loaded += (s, e) =>
            {
                _ = Task.Run(async () => await _viewModel.InitializeAsync());
            };
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void SyncStatusWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel?.Dispose();
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Boolean to Status Converter
    /// </summary>
    public class BoolToStatusConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool success)
            {
                return success ? "成功" : "失败";
            }
            return "未知";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}