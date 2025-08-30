using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 剪贴板历史记录服务接口
    /// </summary>
    public interface IClipboardHistoryService : IDisposable
    {
        /// <summary>
        /// 添加剪贴板项目
        /// </summary>
        /// <param name="item">剪贴板项目</param>
        /// <returns>添加的项目ID</returns>
        Task<int> AddItemAsync(ClipboardItem item);
        
        /// <summary>
        /// 获取所有剪贴板历史记录
        /// </summary>
        /// <param name="limit">限制数量，默认100</param>
        /// <param name="offset">偏移量，默认0</param>
        /// <returns>剪贴板项目列表</returns>
        Task<List<ClipboardItem>> GetAllItemsAsync(int limit = 100, int offset = 0);
        

        
        /// <summary>
        /// 搜索剪贴板历史记录
        /// </summary>
        /// <param name="searchText">搜索文本</param>
        /// <param name="limit">限制数量，默认100</param>
        /// <param name="offset">偏移量，默认0</param>
        /// <returns>匹配的剪贴板项目列表</returns>
        Task<List<ClipboardItem>> SearchItemsAsync(string searchText, int limit = 100, int offset = 0);
        

        
        /// <summary>
        /// 根据ID获取剪贴板项目
        /// </summary>
        /// <param name="id">项目ID</param>
        /// <returns>剪贴板项目，如果不存在则返回null</returns>
        Task<ClipboardItem?> GetItemByIdAsync(int id);
        
        /// <summary>
        /// 更新剪贴板项目
        /// </summary>
        /// <param name="item">要更新的剪贴板项目</param>
        /// <returns>是否更新成功</returns>
        Task<bool> UpdateItemAsync(ClipboardItem item);
        
        /// <summary>
        /// 删除剪贴板项目
        /// </summary>
        /// <param name="id">项目ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteItemAsync(int id);
        
        /// <summary>
        /// 批量删除剪贴板项目
        /// </summary>
        /// <param name="ids">项目ID列表</param>
        /// <returns>删除的项目数量</returns>
        Task<int> DeleteItemsAsync(IEnumerable<int> ids);
        
        /// <summary>
        /// 清空所有剪贴板历史记录
        /// </summary>
        /// <returns>删除的项目数量</returns>
        Task<int> ClearAllItemsAsync();
        

        

        
        /// <summary>
        /// 获取项目总数
        /// </summary>
        /// <returns>项目总数</returns>
        Task<int> GetTotalCountAsync();
        

        
        /// <summary>
        /// 检查是否存在相同内容的项目
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns>如果存在则返回项目，否则返回null</returns>
        Task<ClipboardItem?> FindDuplicateAsync(string content);
    }
}