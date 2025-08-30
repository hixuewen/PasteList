using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasteList.Models
{
    /// <summary>
    /// 剪贴板历史记录项目数据模型
    /// </summary>
    [Table("clipboard_items")]
    public class ClipboardItem
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        /// <summary>
        /// 剪贴板内容
        /// </summary>
        [Required]
        [Column("content")]
        public string Content { get; set; } = string.Empty;
        

        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ClipboardItem()
        {
        }
        
        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        public ClipboardItem(string content) : this()
        {
            Content = content;
        }
    }
}