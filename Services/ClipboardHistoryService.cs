using Microsoft.EntityFrameworkCore;
using PasteList.Data;
using PasteList.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 剪贴板历史记录服务实现
    /// </summary>
    public class ClipboardHistoryService : IClipboardHistoryService, IDisposable
    {
        private readonly ClipboardDbContext _context;
        private bool _disposed = false;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ClipboardHistoryService()
        {
            _context = new ClipboardDbContext();
            // 确保数据库已创建
            _context.EnsureCreated();
        }
        
        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        /// <param name="context">数据库上下文</param>
        public ClipboardHistoryService(ClipboardDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        
        /// <summary>
        /// 添加剪贴板项目
        /// </summary>
        /// <param name="item">剪贴板项目</param>
        /// <returns>添加的项目</returns>
        public async Task<ClipboardItem?> AddItemAsync(ClipboardItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            try
            {
                // 检查是否存在相同内容的项目
                var existingItem = await FindDuplicateAsync(item.Content);
                if (existingItem != null)
                {
                    // 如果存在相同内容，返回null表示未添加新项目
                    return null;
                }

                // 添加新项目
                _context.ClipboardItems.Add(item);
                await _context.SaveChangesAsync();

                return item;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"添加剪贴板项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 获取所有剪贴板历史记录
        /// </summary>
        /// <param name="limit">限制数量，默认100</param>
        /// <param name="offset">偏移量，默认0</param>
        /// <returns>剪贴板项目列表</returns>
        public async Task<List<ClipboardItem>> GetAllItemsAsync(int limit = 100, int offset = 0)
        {
            try
            {
                return await _context.ClipboardItems
                    .OrderByDescending(x => x.Id)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取剪贴板历史记录失败: {ex.Message}", ex);
            }
        }
        

        
        /// <summary>
        /// 搜索剪贴板历史记录
        /// </summary>
        /// <param name="searchText">搜索文本</param>
        /// <param name="limit">限制数量，默认100</param>
        /// <param name="offset">偏移量，默认0</param>
        /// <returns>匹配的剪贴板项目列表</returns>
        public async Task<List<ClipboardItem>> SearchItemsAsync(string searchText, int limit = 100, int offset = 0)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return new List<ClipboardItem>();
            
            try
            {
                return await _context.ClipboardItems
                    .Where(x => x.Content.Contains(searchText))
                    .OrderByDescending(x => x.Id)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"搜索剪贴板历史记录失败: {ex.Message}", ex);
            }
        }
        

        
        /// <summary>
        /// 根据ID获取剪贴板项目
        /// </summary>
        /// <param name="id">项目ID</param>
        /// <returns>剪贴板项目，如果不存在则返回null</returns>
        public async Task<ClipboardItem?> GetItemByIdAsync(int id)
        {
            try
            {
                return await _context.ClipboardItems
                    .FirstOrDefaultAsync(x => x.Id == id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"根据ID获取剪贴板项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 更新剪贴板项目
        /// </summary>
        /// <param name="item">要更新的剪贴板项目</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdateItemAsync(ClipboardItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            
            try
            {
                _context.ClipboardItems.Update(item);
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"更新剪贴板项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 删除剪贴板项目
        /// </summary>
        /// <param name="id">项目ID</param>
        /// <returns>是否删除成功</returns>
        public async Task<bool> DeleteItemAsync(int id)
        {
            try
            {
                var item = await _context.ClipboardItems.FindAsync(id);
                if (item == null)
                    return false;
                
                _context.ClipboardItems.Remove(item);
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"删除剪贴板项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 批量删除剪贴板项目
        /// </summary>
        /// <param name="ids">项目ID列表</param>
        /// <returns>删除的项目数量</returns>
        public async Task<int> DeleteItemsAsync(IEnumerable<int> ids)
        {
            if (ids == null || !ids.Any())
                return 0;
            
            try
            {
                var items = await _context.ClipboardItems
                    .Where(x => ids.Contains(x.Id))
                    .ToListAsync();
                
                if (items.Any())
                {
                    _context.ClipboardItems.RemoveRange(items);
                    return await _context.SaveChangesAsync();
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"批量删除剪贴板项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 清空所有剪贴板历史记录
        /// </summary>
        /// <returns>删除的项目数量</returns>
        public async Task<int> ClearAllItemsAsync()
        {
            try
            {
                var allItems = await _context.ClipboardItems.ToListAsync();
                if (allItems.Any())
                {
                    _context.ClipboardItems.RemoveRange(allItems);
                    return await _context.SaveChangesAsync();
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"清空剪贴板历史记录失败: {ex.Message}", ex);
            }
        }
        

        

        
        /// <summary>
        /// 获取项目总数
        /// </summary>
        /// <returns>项目总数</returns>
        public async Task<int> GetTotalCountAsync()
        {
            try
            {
                return await _context.ClipboardItems.CountAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"获取项目总数失败: {ex.Message}", ex);
            }
        }
        

        
        /// <summary>
        /// 检查是否存在相同内容的项目
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns>如果存在则返回项目，否则返回null</returns>
        public async Task<ClipboardItem?> FindDuplicateAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;
            
            try
            {
                return await _context.ClipboardItems
                    .FirstOrDefaultAsync(x => x.Content == content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"查找重复项目失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context?.Dispose();
                _disposed = true;
            }
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~ClipboardHistoryService()
        {
            Dispose(false);
        }
    }
}