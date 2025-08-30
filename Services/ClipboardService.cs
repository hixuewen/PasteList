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
        
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        
        #endregion
        
        private readonly Window _window;
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
        public ClipboardService(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }
        
        /// <summary>
        /// 开始监听剪贴板
        /// </summary>
        public void StartListening()
        {
            if (_isListening) return;
            
            try
            {
                // 获取窗口句柄
                _hwndSource = PresentationSource.FromVisual(_window) as HwndSource;
                if (_hwndSource == null)
                {
                    throw new InvalidOperationException("无法获取窗口句柄");
                }
                
                // 添加消息钩子
                _hwndSource.AddHook(WndProc);
                
                // 注册剪贴板格式监听器
                if (!AddClipboardFormatListener(_hwndSource.Handle))
                {
                    throw new InvalidOperationException("无法注册剪贴板监听器");
                }
                
                _isListening = true;
                
                // 获取当前剪贴板内容作为初始状态
                var currentContent = GetCurrentClipboardContent();
                if (currentContent != null)
                {
                    _lastClipboardContent = currentContent.Content;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"启动剪贴板监听失败: {ex.Message}", ex);
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
                var clipboardItem = GetCurrentClipboardContent();
                if (clipboardItem != null)
                {
                    // 检查内容是否与上次相同，避免重复处理
                    if (_lastClipboardContent != clipboardItem.Content)
                    {
                        _lastClipboardContent = clipboardItem.Content;
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