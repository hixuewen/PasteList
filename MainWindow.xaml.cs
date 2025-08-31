using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
// 使用 WPF 原生方式实现托盘功能
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
        private bool _isClosing = false;
        private HwndSource? _source;
        #region Windows API 声明
        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        

        
        /// <summary>
        /// 重写窗口消息处理，处理托盘图标事件
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // 获取窗口句柄并添加消息钩子
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source.AddHook(WndProc);
            
            // 注册热键
            RegisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_ID, MOD_ALT, VK_Z);
        }
        
        /// <summary>
        /// 窗口消息处理
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_HOTKEY:
                    if (wParam.ToInt32() == HOTKEY_ID)
                    {
                        if (IsVisible)
                        {
                            WindowState = WindowState.Minimized;
                        }
                        else
                        {
                            RestoreWindow();
                        }
                        handled = true;
                    }
                    break;
                    

            }
            
            return IntPtr.Zero;
        }
        
        // 热键修饰符
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        
        // 虚拟键码
        private const uint VK_Z = 0x5A;
        
        // 热键ID
        private const int HOTKEY_ID = 9000;
        
        // Windows消息
        private const int WM_HOTKEY = 0x0312;
        
        #endregion
        
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
            
            // 订阅窗口状态变化事件
            StateChanged += MainWindow_StateChanged;
            
            // 托盘图标已在XAML中定义，无需额外初始化
            
            // 窗口加载完成后初始化ViewModel
            Loaded += MainWindow_Loaded;
        }
        


        /// <summary>
        /// 窗口加载完成事件处理
        /// 初始化ViewModel的数据加载和注册全局快捷键
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载历史记录数据
                await _viewModel.LoadHistoryAsync();
                
                // 热键注册已在 OnSourceInitialized 中处理
                
                // 设置初始状态消息
                _viewModel.StatusMessage = "应用程序已就绪，按 Alt+Z 可唤起窗口";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// 如果不是真正关闭，则最小化到托盘；否则清理资源
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                // 取消关闭，改为最小化到托盘
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                Hide();
                return;
            }
            
            try
            {
                // 注销全局快捷键
                UnregisterGlobalHotKey();
                
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
        /// 窗口状态变化事件处理
        /// 当窗口最小化时隐藏到托盘
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }
        

        
        /// <summary>
        /// 恢复窗口显示
        /// </summary>
        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
        
        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            _isClosing = true;
            Application.Current.Shutdown();
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
        
        #region 全局快捷键功能
        
        /// <summary>
        /// 注册全局快捷键 Alt+Z
        /// </summary>
        private void RegisterGlobalHotKey()
        {
            try
            {
                // 获取窗口句柄
                var helper = new WindowInteropHelper(this);
                var hwnd = helper.Handle;
                
                if (hwnd != IntPtr.Zero)
                {
                    // 注册热键 Alt+Z
                    bool success = RegisterHotKey(hwnd, HOTKEY_ID, MOD_ALT, VK_Z);
                    
                    if (success)
                    {
                        // 添加消息钩子
                        _source = HwndSource.FromHwnd(hwnd);
                        _source?.AddHook(WndProc);
                        
                        System.Diagnostics.Debug.WriteLine("全局快捷键 Alt+Z 注册成功");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("全局快捷键 Alt+Z 注册失败");
                    }
                }
                else
                {
                    // 如果窗口句柄还未创建，延迟注册
                    this.SourceInitialized += (s, e) => RegisterGlobalHotKey();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册全局快捷键时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 注销全局快捷键
        /// </summary>
        private void UnregisterGlobalHotKey()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                var hwnd = helper.Handle;
                
                if (hwnd != IntPtr.Zero)
                {
                    // 注销热键
                    UnregisterHotKey(hwnd, HOTKEY_ID);
                    
                    // 移除消息钩子
                    _source?.RemoveHook(WndProc);
                    
                    System.Diagnostics.Debug.WriteLine("全局快捷键已注销");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注销全局快捷键时发生错误: {ex.Message}");
            }
        }
        

        
        /// <summary>
        /// 处理全局快捷键按下事件
        /// 唤起主窗口
        /// </summary>
        private void OnGlobalHotKeyPressed()
        {
            try
            {
                // 如果窗口最小化，恢复窗口
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                
                // 激活窗口并置于前台
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();
                
                System.Diagnostics.Debug.WriteLine("通过全局快捷键唤起窗口");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"唤起窗口时发生错误: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 托盘菜单事件处理
        

        
        /// <summary>
        /// 处理"显示窗口"菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            RestoreWindow();
        }
        
        /// <summary>
        /// 处理"退出"菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }
        
        /// <summary>
        /// 处理托盘图标双击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            RestoreWindow();
        }
        
        #endregion
    }
}