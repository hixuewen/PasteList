using PasteList.Models;
using System;

namespace PasteList.Services
{
    /// <summary>
    /// 剪贴板监听服务接口
    /// </summary>
    public interface IClipboardService : IDisposable
    {
        /// <summary>
        /// 剪贴板内容变化事件
        /// </summary>
        event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
        
        /// <summary>
        /// 开始监听剪贴板
        /// </summary>
        void StartListening();
        
        /// <summary>
        /// 停止监听剪贴板
        /// </summary>
        void StopListening();
        
        /// <summary>
        /// 获取当前剪贴板内容
        /// </summary>
        /// <returns>剪贴板项目，如果无内容则返回null</returns>
        ClipboardItem? GetCurrentClipboardContent();
        
        /// <summary>
        /// 设置剪贴板内容
        /// </summary>
        /// <param name="content">要设置的内容</param>
        void SetClipboardContent(string content);
        
        /// <summary>
        /// 检查是否正在监听
        /// </summary>
        bool IsListening { get; }
    }
    
    /// <summary>
    /// 剪贴板变化事件参数
    /// </summary>
    public class ClipboardChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 新的剪贴板项目
        /// </summary>
        public ClipboardItem ClipboardItem { get; }
        

        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="clipboardItem">剪贴板项目</param>
        public ClipboardChangedEventArgs(ClipboardItem clipboardItem)
        {
            ClipboardItem = clipboardItem;
        }
    }
}