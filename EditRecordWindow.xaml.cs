using System;
using System.Windows;
using PasteList.Models;

namespace PasteList
{
    /// <summary>
    /// 编辑剪贴板记录窗口
    /// </summary>
    public partial class EditRecordWindow : Window
    {
        /// <summary>
        /// 原始剪贴板项目
        /// </summary>
        public ClipboardItem OriginalItem { get; }

        /// <summary>
        /// 编辑后的内容（仅在保存后有效）
        /// </summary>
        public string EditedContent { get; private set; } = string.Empty;

        /// <summary>
        /// 用户是否点击了保存
        /// </summary>
        public bool IsSaved { get; private set; } = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="item">要编辑的剪贴板项目</param>
        public EditRecordWindow(ClipboardItem item)
        {
            InitializeComponent();

            OriginalItem = item ?? throw new ArgumentNullException(nameof(item));

            // 填充当前内容
            ContentTextBox.Text = item.Content;

            // 加载后全选文本方便快速替换
            this.Loaded += (s, e) =>
            {
                ContentTextBox.Focus();
                ContentTextBox.SelectAll();
            };
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newContent = ContentTextBox.Text;

            // 内容未变化，视为取消
            if (newContent == OriginalItem.Content)
            {
                IsSaved = false;
                Close();
                return;
            }

            // 内容不能为空
            if (string.IsNullOrWhiteSpace(newContent))
            {
                MessageBox.Show("内容不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditedContent = newContent;
            IsSaved = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = false;
            Close();
        }
    }
}
