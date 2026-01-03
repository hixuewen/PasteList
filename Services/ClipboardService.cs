using PasteList.Models;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.IO;

namespace PasteList.Services
{
    /// <summary>
    /// 剪贴板监听服务实现
    /// </summary>
    public class ClipboardService : IClipboardService, IDisposable
    {
        #region Windows API 声明

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        
        #endregion
        
        private readonly Window _window;
        private readonly ILoggerService? _logger;
        private HwndSource? _hwndSource;
        private bool _isListening;
        private string? _lastClipboardContent;
        
        /// <summary>
        /// 剪贴板内容变化事件
        /// </summary>
        public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
        
        /// <summary>
        /// 检查是否正在监听
        /// </summary>
        public bool IsListening => _isListening;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="window">主窗口实例</param>
        /// <param name="logger">日志服务</param>
        public ClipboardService(Window window, ILoggerService? logger = null)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _logger = logger;
        }
        
        /// <summary>
        /// 开始监听剪贴板
        /// </summary>
        public void StartListening()
        {
            if (_isListening)
            {
                _logger?.LogDebug("剪贴板监听器已在运行，跳过启动");
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger?.LogInfo("开始启动剪贴板监听器");

                // 获取窗口句柄，增加重试机制和状态检查
                int retryCount = 0;
                int maxRetries = 20; // 增加重试次数
                int retryInterval = 200; // 增加间隔时间

                _logger?.LogDebug("开始获取窗口句柄...");

                // 检查窗口状态
                if (!_window.IsLoaded)
                {
                    _logger?.LogWarning("窗口尚未完全加载，等待加载完成...");
                    while (!_window.IsLoaded && retryCount < 10)
                    {
                        System.Threading.Thread.Sleep(200);
                        retryCount++;
                    }
                    if (!_window.IsLoaded)
                    {
                        throw new InvalidOperationException("窗口加载超时，请确保窗口已完全显示");
                    }
                }

                retryCount = 0;
                IntPtr windowHandle = IntPtr.Zero;

                // 使用WindowInteropHelper获取窗口句柄，更可靠
                while (windowHandle == IntPtr.Zero && retryCount < maxRetries)
                {
                    var windowInteropHelper = new WindowInteropHelper(_window);
                    windowHandle = windowInteropHelper.Handle;

                    if (windowHandle == IntPtr.Zero)
                    {
                        _logger?.LogDebug($"使用WindowInteropHelper获取窗口句柄，第 {retryCount + 1} 次失败，等待 {retryInterval}ms 后重试");
                        System.Threading.Thread.Sleep(retryInterval);
                        retryCount++;
                    }
                }

                if (windowHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"无法获取窗口句柄，已重试 {maxRetries} 次。请确保窗口已完全加载且可见。");
                }

                // 创建HwndSource
                _hwndSource = HwndSource.FromHwnd(windowHandle);
                if (_hwndSource == null)
                {
                    throw new InvalidOperationException("无法从窗口句柄创建HwndSource，请确保窗口状态正常。");
                }

                _logger?.LogDebug($"成功获取窗口句柄: {_hwndSource.Handle}");

                // 检查窗口句柄是否有效
                if (_hwndSource.Handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("窗口句柄无效，请确保窗口已完全加载且可见");
                }

                // 额外的窗口状态检查
                if (!_window.IsVisible)
                {
                    _logger?.LogWarning("窗口当前不可见，但这可能不影响剪贴板监听");
                }

                // 尝试验证窗口句柄是否真的有效
                if (!IsWindowHandleValid(_hwndSource.Handle))
                {
                    throw new InvalidOperationException("窗口句柄验证失败，可能窗口已关闭或无效");
                }

                // 添加消息钩子
                _hwndSource.AddHook(WndProc);
                _logger?.LogDebug("已添加窗口消息钩子");

                // 注册剪贴板格式监听器
                if (!AddClipboardFormatListener(_hwndSource.Handle))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"无法注册剪贴板监听器，错误代码: {error}");
                }

                _isListening = true;

                // 获取当前剪贴板内容作为初始状态
                try
                {
                    var currentContent = GetCurrentClipboardContent();
                    if (currentContent != null)
                    {
                        _lastClipboardContent = currentContent.Content;
                        _logger?.LogDebug($"已获取初始剪贴板内容，长度: {currentContent.Content.Length}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"获取初始剪贴板内容失败: {ex.Message}");
                }

                _logger?.LogInfo("剪贴板监听器启动成功");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动剪贴板监听器失败");

                // 清理已分配的资源
                try
                {
                    if (_hwndSource != null)
                    {
                        _hwndSource.RemoveHook(WndProc);
                    }
                }
                catch { }

                _isListening = false;

                // 根据异常类型提供更友好的错误信息
                string userMessage = GetUserFriendlyErrorMessage(ex);
                throw new InvalidOperationException(userMessage, ex);
            }
            finally
            {
                stopwatch.Stop();
                _logger?.LogPerformance("启动剪贴板监听器", stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 停止监听剪贴板
        /// </summary>
        public void StopListening()
        {
            if (!_isListening) return;
            
            try
            {
                if (_hwndSource != null)
                {
                    // 移除剪贴板格式监听器
                    RemoveClipboardFormatListener(_hwndSource.Handle);
                    
                    // 移除消息钩子
                    _hwndSource.RemoveHook(WndProc);
                }
                
                _isListening = false;
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                System.Diagnostics.Debug.WriteLine($"停止剪贴板监听时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取当前剪贴板内容
        /// </summary>
        /// <returns>剪贴板项目，如果无内容则返回null</returns>
        public ClipboardItem? GetCurrentClipboardContent()
        {
            try
            {
                if (!Clipboard.ContainsData(DataFormats.Text) && 
                    !Clipboard.ContainsData(DataFormats.Bitmap) && 
                    !Clipboard.ContainsData(DataFormats.FileDrop))
                {
                    return null;
                }
                
                // 检查文本内容
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new ClipboardItem(text);
                    }
                }
                
                // 检查图片内容
                if (Clipboard.ContainsImage())
                {
                    var image = Clipboard.GetImage();
                    if (image != null)
                    {
                        // 将图片转换为Base64字符串存储
                        string base64Image = ConvertImageToBase64(image);
                        return new ClipboardItem(base64Image);
                    }
                }
                
                // 检查文件内容
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count > 0)
                    {
                        // 将文件路径列表转换为字符串
                        string fileList = string.Join(";", files.Cast<string>());
                        return new ClipboardItem(fileList);
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取剪贴板内容时发生错误: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 设置剪贴板内容
        /// </summary>
        /// <param name="content">要设置的内容</param>
        public void SetClipboardContent(string content)
        {
            try
            {
                if (!string.IsNullOrEmpty(content))
                {
                    Clipboard.SetText(content);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"设置剪贴板内容失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 获取用户友好的错误信息
        /// </summary>
        /// <param name="ex">异常</param>
        /// <returns>用户友好的错误信息</returns>
        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            if (ex.Message.Contains("窗口句柄") || ex.Message.Contains("窗口加载"))
            {
                return "启动剪贴板监听失败：窗口尚未准备就绪。\n\n解决方法：\n1. 确保应用程序窗口已完全显示\n2. 稍后再试\n3. 如果问题持续存在，请重启应用程序";
            }
            else if (ex.Message.Contains("剪贴板监听器") && ex.Message.Contains("错误代码"))
            {
                return "启动剪贴板监听失败：系统权限不足。\n\n解决方法：\n1. 请以管理员身份运行应用程序\n2. 检查其他应用程序是否正在使用剪贴板\n3. 重启计算机后重试";
            }
            else
            {
                return $"启动剪贴板监听失败：{ex.Message}\n\n请重启应用程序或联系技术支持。";
            }
        }

        /// <summary>
        /// 验证窗口句柄是否有效
        /// </summary>
        /// <param name="handle">窗口句柄</param>
        /// <returns>是否有效</returns>
        private bool IsWindowHandleValid(IntPtr handle)
        {
            try
            {
                return handle != IntPtr.Zero && IsWindow(handle);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"验证窗口句柄时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 窗口消息处理程序
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="msg">消息</param>
        /// <param name="wParam">wParam</param>
        /// <param name="lParam">lParam</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理结果</returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                handled = true;
            }
            
            return IntPtr.Zero;
        }
        
        /// <summary>
        /// 处理剪贴板变化事件
        /// </summary>
        private void OnClipboardChanged()
        {
            try
            {
                // 延迟一小段时间，确保剪贴板数据已完全准备好
                System.Threading.Thread.Sleep(50);

                var clipboardItem = GetCurrentClipboardContent();
                if (clipboardItem != null)
                {
                    // 检查内容是否与上次相同，避免重复处理
                    if (_lastClipboardContent != clipboardItem.Content)
                    {
                        _lastClipboardContent = clipboardItem.Content;
                        System.Diagnostics.Debug.WriteLine($"检测到剪贴板变化: {clipboardItem.Content?.Substring(0, Math.Min(50, clipboardItem.Content?.Length ?? 0))}...");
                        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(clipboardItem));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理剪贴板变化时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 将图片转换为Base64字符串
        /// </summary>
        /// <param name="image">图片对象</param>
        /// <returns>Base64字符串</returns>
        private string ConvertImageToBase64(BitmapSource image)
        {
            try
            {
                using var stream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(stream);
                
                byte[] imageBytes = stream.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转换图片为Base64时发生错误: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopListening();
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~ClipboardService()
        {
            Dispose();
        }
    }
}
