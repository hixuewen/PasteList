using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace PasteList.Models
{
    /// <summary>
    /// 剪贴板历史记录项目数据模型
    /// </summary>
    [Table("clipboard_items")]
    public class ClipboardItem : INotifyPropertyChanged
    {
        private int _id;
        private string _content = string.Empty;

        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [Column("id")]
        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 剪贴板内容
        /// </summary>
        [Required]
        [Column("content")]
        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

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

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}