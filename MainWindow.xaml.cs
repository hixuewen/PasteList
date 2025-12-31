using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
using PasteList.Models;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

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
        
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        

        
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
                        HandleGlobalHotKey();
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
        
        private MainWindowViewModel? _viewModel;
        private IClipboardService? _clipboardService;
        private IClipboardHistoryService? _historyService;
        private ClipboardDbContext? _dbContext;
        private IStartupService? _startupService;
        private IAuthService? _authService;
        private ILoggerService? _logger;

        /// <summary>
        /// 初始化MainWindow，设置数据上下文和服务
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // 设置窗口启动时在屏幕中央
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // 异步初始化
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载时进行异步初始化
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeAsync();

                // 加载历史记录数据
                if (_viewModel != null)
                {
                    await _viewModel.LoadHistoryAsync();
                    // 设置初始状态消息
                    _viewModel.StatusMessage = "应用程序已就绪，按 Alt+Z 可唤起窗口";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// 异步初始化应用程序
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                // 初始化日志服务
                _logger = new LoggerService();
                _logger.LogApplicationStart();

                // 初始化数据库上下文
                _dbContext = new ClipboardDbContext();
                _logger.LogInfo("数据库上下文初始化完成");

                // 确保数据库已创建
                _dbContext.Database.EnsureCreated();
                _logger.LogInfo("数据库创建或连接成功");

            // 初始化服务
            _clipboardService = new ClipboardService(this, _logger);
            _logger.LogInfo("剪贴板服务初始化完成");

            _historyService = new ClipboardHistoryService(_dbContext);
            _logger.LogInfo("历史记录服务初始化完成");

            _startupService = new StartupService();
            _logger.LogInfo("启动服务初始化完成");

            // 初始化认证服务
            _authService = new AuthService(_logger);
            _logger.LogInfo("认证服务初始化完成");

            // 初始化ViewModel
            _viewModel = new MainWindowViewModel(_clipboardService, _historyService, _logger);
            _logger.LogInfo("ViewModel初始化完成");

            // 设置数据上下文
            DataContext = _viewModel;

            // 订阅窗口关闭事件
            Closing += MainWindow_Closing;

            // 订阅窗口状态变化事件
            StateChanged += MainWindow_StateChanged;

            // 托盘图标已在XAML中定义，无需额外初始化

            // 设置托盘图标 - 创建一个简单的图标
            try
            {
                TrayIcon.Icon = CreateSimpleIcon();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "托盘图标创建失败");
            }

            // 注册全局热键
            RegisterGlobalHotKey();

            _logger.LogInfo("MainWindow 初始化完成");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MainWindow 初始化过程中发生错误");
            MessageBox.Show($"应用程序初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }
        


        
  
        /// <summary>
        /// 窗口关闭事件处理
        /// 如果不是真正关闭，则最小化到托盘；否则清理资源
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                // 取消关闭，改为最小化到托盘
                e.Cancel = true;
                WindowState = WindowState.Minimized;
                Hide();
                _logger?.LogUserAction("窗口最小化到托盘");
                return;
            }

            try
            {
                _logger?.LogInfo("开始清理应用程序资源");

                // 注销全局快捷键
                UnregisterGlobalHotKey();
                _logger?.LogDebug("全局快捷键已注销");

                // 停止剪贴板监听
                if (_clipboardService != null)
                {
                    _clipboardService.StopListening();
                    _logger?.LogDebug("剪贴板监听已停止");
                }

                // 释放ViewModel资源
                _viewModel?.Dispose();
                _logger?.LogDebug("ViewModel资源已释放");

                // 释放历史服务资源
                _historyService?.Dispose();
                _logger?.LogDebug("历史记录服务资源已释放");

                // 释放数据库上下文资源
                _dbContext?.Dispose();
                _logger?.LogDebug("数据库上下文资源已释放");

                // 释放日志服务
                _logger?.LogApplicationShutdown();
                if (_logger is IDisposable disposableLogger)
                {
                    disposableLogger.Dispose();
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止窗口关闭
                System.Diagnostics.Debug.WriteLine($"关闭窗口时发生错误: {ex.Message}");
                _logger?.LogError(ex, "关闭窗口时发生错误");
            }
        }

        /// <summary>
        /// 窗口状态变化事件处理
        /// 当窗口最小化时隐藏到托盘
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                _logger?.LogDebug("窗口已最小化并隐藏");
            }
        }
        

        
        /// <summary>
        /// 恢复窗口显示
        /// </summary>
        private void RestoreWindow()
        {
            try
            {
                _logger?.LogUserAction("恢复窗口显示");

                // 确保ViewModel已初始化
                if (_viewModel == null)
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    _logger?.LogWarning("恢复窗口时ViewModel为null");
                    return;
                }

                // 在显示窗口前记录当前活动窗口
                _viewModel.RecordPreviousActiveWindow();

                Show();
                WindowState = WindowState.Normal;
                Activate();

                _logger?.LogDebug("窗口已恢复显示");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "恢复窗口显示时发生错误");
                throw;
            }
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
            if (_viewModel?.SelectedItem != null && _viewModel.DoubleClickItemCommand.CanExecute(null))
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
        
        #region 全局快捷键处理
        
        /// <summary>
        /// 处理全局快捷键事件
        /// 根据当前窗口状态决定是显示还是隐藏窗口
        /// </summary>
        private void HandleGlobalHotKey()
        {
            try
            {
                // 获取当前前台窗口句柄
                IntPtr foregroundWindow = GetForegroundWindow();
                IntPtr thisWindowHandle = new WindowInteropHelper(this).Handle;
                
                // 检查窗口是否最小化到托盘（不可见且WindowState为Minimized）
                bool isMinimizedToTray = !IsVisible && WindowState == WindowState.Minimized;
                
                // 检查窗口是否在任务栏但不在前台
                bool isInTaskbarButNotForeground = IsVisible && 
                                                   WindowState != WindowState.Minimized && 
                                                   foregroundWindow != thisWindowHandle;
                
                // 检查窗口是否在前台
                bool isInForeground = IsVisible && 
                                    WindowState != WindowState.Minimized && 
                                    foregroundWindow == thisWindowHandle;
                
                if (isMinimizedToTray || isInTaskbarButNotForeground)
                {
                    // 窗口在托盘或任务栏但非前台：显示并激活窗口
                    RestoreWindow();
                }
                else if (isInForeground)
                {
                    // 窗口在前台：最小化到托盘
                    WindowState = WindowState.Minimized;
                }
                else
                {
                    // 其他情况（理论上不应该发生）：显示窗口
                    RestoreWindow();
                }
                
                System.Diagnostics.Debug.WriteLine($"快捷键处理: IsVisible={IsVisible}, WindowState={WindowState}, IsForeground={isInForeground}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理全局快捷键时发生错误: {ex.Message}");
                // 出错时默认尝试显示窗口
                RestoreWindow();
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
            _logger?.LogUserAction("点击托盘菜单 - 显示窗口");
            RestoreWindow();
        }

        /// <summary>
        /// 处理"设置"菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogUserAction("点击托盘菜单 - 设置");

                if (_startupService == null)
                {
                    MessageBox.Show("启动服务未初始化，无法打开设置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_authService == null)
                {
                    MessageBox.Show("认证服务未初始化，无法打开设置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 创建设置窗口
                var settingsWindow = new SettingsWindow(_startupService, _authService, _logger);
                settingsWindow.Owner = this;

                // 显示设置窗口（模态对话框）
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "打开设置窗口时发生错误");
                System.Diagnostics.Debug.WriteLine($"打开设置窗口时发生错误: {ex.Message}");
                MessageBox.Show($"打开设置窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理"退出"菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogUserAction("点击托盘菜单 - 退出应用程序");
            ExitApplication();
        }

        /// <summary>
        /// 处理托盘图标双击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            _logger?.LogUserAction("双击托盘图标");
            RestoreWindow();
        }

        #endregion

        #region 图标处理
        
        /// <summary>
        /// 创建一个自定义的剪贴板主题图标
        /// </summary>
        /// <returns>自定义图标</returns>
        private System.Drawing.Icon CreateSimpleIcon()
        {
            try
            {
                // 首先尝试创建高级剪贴板图标
                return CreateClipboardIcon();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"无法创建高级图标: {ex.Message}");
                try
                {
                    // 如果高级图标失败，尝试创建简单版本
                    return CreateSimpleClipboardIcon();
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"无法创建简单图标: {ex2.Message}");
                    // 最后返回系统图标
                    return System.Drawing.SystemIcons.Application;
                }
            }
        }

        /// <summary>
        /// 创建剪贴板主题的自定义图标
        /// </summary>
        /// <returns>剪贴板图标</returns>
        private System.Drawing.Icon CreateClipboardIcon()
        {
            // 创建32x32的位图
            using (var bitmap = new System.Drawing.Bitmap(32, 32))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                // 设置高质量渲染
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // 绘制渐变背景（圆形，现代渐变效果）
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddEllipse(2, 2, 28, 28);
                    using (var brush = new System.Drawing.Drawing2D.PathGradientBrush(path))
                    {
                        brush.CenterColor = System.Drawing.Color.FromArgb(70, 130, 230);  // 亮蓝色
                        brush.SurroundColors = new System.Drawing.Color[] { System.Drawing.Color.FromArgb(0, 90, 180) };  // 深蓝色
                        graphics.FillEllipse(brush, 2, 2, 28, 28);
                    }
                }

                // 绘制背景边框
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 255, 255, 100), 1))
                {
                    graphics.DrawEllipse(pen, 3, 3, 26, 26);
                }

                // 绘制剪贴板图标（白色，带阴影效果）
                // 阴影
                using (var shadowBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(50, 0, 0, 0)))
                {
                    var shadowRect = new System.Drawing.RectangleF(9, 8, 16, 20);
                    graphics.FillRectangle(shadowBrush, shadowRect);
                }

                // 剪贴板主体
                using (var clipBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                using (var clipPen = new System.Drawing.Pen(System.Drawing.Color.White, 1))
                {
                    var clipRect = new System.Drawing.RectangleF(8, 7, 16, 20);
                    graphics.FillRectangle(clipBrush, clipRect);
                    graphics.DrawRectangle(clipPen, clipRect.X, clipRect.Y, clipRect.Width, clipRect.Height);

                    // 剪贴板夹子（金属质感）
                    using (var clipperBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 200, 200)))
                    {
                        graphics.FillEllipse(clipperBrush, 14, 5, 4, 4);
                        graphics.DrawEllipse(clipPen, 14, 5, 4, 4);
                    }

                    // 文档线条（表示内容，更精细）
                    graphics.DrawLine(clipPen, 11, 12, 21, 12);
                    graphics.DrawLine(clipPen, 11, 15, 21, 15);
                    graphics.DrawLine(clipPen, 11, 18, 18, 18);

                    // 小点装饰
                    graphics.FillEllipse(clipBrush, 20, 18, 2, 2);
                }

                // 添加光泽效果
                using (var glossPath = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    glossPath.AddEllipse(6, 4, 12, 12);
                    using (var glossBrush = new System.Drawing.Drawing2D.PathGradientBrush(glossPath))
                    {
                        glossBrush.CenterColor = System.Drawing.Color.FromArgb(80, 255, 255, 255);
                        glossBrush.SurroundColors = new System.Drawing.Color[] { System.Drawing.Color.Transparent };
                        graphics.FillEllipse(glossBrush, 6, 4, 12, 12);
                    }
                }

                // 转换为图标
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }

        /// <summary>
        /// 创建简单的剪贴板图标（兼容性更好）
        /// </summary>
        /// <returns>简单剪贴板图标</returns>
        private System.Drawing.Icon CreateSimpleClipboardIcon()
        {
            // 创建32x32的位图
            using (var bitmap = new System.Drawing.Bitmap(32, 32))
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                // 设置高质量渲染
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 绘制背景（圆形，纯蓝色）
                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215)))
                {
                    graphics.FillEllipse(brush, 2, 2, 28, 28);
                }

                // 绘制剪贴板图标（白色）
                using (var clipBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                {
                    // 剪贴板主体
                    var clipRect = new System.Drawing.RectangleF(8, 8, 16, 18);
                    graphics.FillRectangle(clipBrush, clipRect);

                    // 剪贴板夹子
                    graphics.FillRectangle(clipBrush, 14, 6, 4, 3);

                    // 文档线条
                    using (var lineBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 90, 180)))
                    {
                        graphics.DrawLine(new System.Drawing.Pen(lineBrush, 1), 11, 12, 21, 12);
                        graphics.DrawLine(new System.Drawing.Pen(lineBrush, 1), 11, 15, 21, 15);
                        graphics.DrawLine(new System.Drawing.Pen(lineBrush, 1), 11, 18, 18, 18);
                    }
                }

                // 转换为图标
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }
        
        #endregion
    }
}
