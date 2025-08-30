using Microsoft.EntityFrameworkCore;
using PasteList.Models;
using System;
using System.IO;

namespace PasteList.Data
{
    /// <summary>
    /// 剪贴板数据库上下文类
    /// </summary>
    public class ClipboardDbContext : DbContext
    {
        /// <summary>
        /// 剪贴板项目数据集
        /// </summary>
        public DbSet<ClipboardItem> ClipboardItems { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ClipboardDbContext()
        {
        }
        
        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        /// <param name="options">数据库上下文选项</param>
        public ClipboardDbContext(DbContextOptions<ClipboardDbContext> options) : base(options)
        {
        }
        
        /// <summary>
        /// 配置数据库连接
        /// </summary>
        /// <param name="optionsBuilder">选项构建器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // 获取应用程序数据目录
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appDataPath, "PasteList");
                
                // 确保目录存在
                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }
                
                // 配置SQLite数据库连接字符串
                string dbPath = Path.Combine(appFolder, "clipboard.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
        
        /// <summary>
        /// 配置实体模型
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // 配置ClipboardItem实体
            modelBuilder.Entity<ClipboardItem>(entity =>
            {
                // 设置主键
                entity.HasKey(e => e.Id);
                
                // 配置属性
                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasMaxLength(10000); // 限制内容最大长度
            });
        }
        
        /// <summary>
        /// 确保数据库已创建
        /// </summary>
        public void EnsureCreated()
        {
            Database.EnsureCreated();
        }
    }
}