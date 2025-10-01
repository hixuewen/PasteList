using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PasteList.Models;
using PasteList.Services;

namespace PasteList.ViewModels
{
    /// <summary>
    /// 主窗口的ViewModel，实现MVVM模式
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IClipboardService _clipboardService;
        private readonly IClipboardHistoryService _historyService;
        private readonly ILoggerService _logger;
        private bool _disposed = false;
        
        private ObservableCollection<ClipboardItem> _clipboardItems;
        private ClipboardItem? _selectedItem;
        private string _searchText = string.Empty;
        private bool _isListening = false;
        private int _totalCount = 0;
        private string _statusMessage = "就绪";
        
        // 记录前一个活动窗口的句柄
        private IntPtr _previousActiveWindow = IntPtr.Zero;
        
        /// <summary>
        /// 构造函数（仅用于设计时）
        /// </summary>
        public MainWindowViewModel()
        {
            // 设计时构造函数，不初始化服务
            _clipboardItems = new ObservableCollection<ClipboardItem>();
            
            // 只有在运行时才初始化命令
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                throw new InvalidOperationException("请使用带参数的构造函数");
            }
        }
        
        /// <summary>
        /// 带参数的构造函数（用于测试）
        /// </summary>
        /// <param name="clipboardService">剪贴板服务</param>
        /// <param name="historyService">历史记录服务</param>
        /// <param name="logger">日志服务</param>
        public MainWindowViewModel(IClipboardService clipboardService, IClipboardHistoryService historyService, ILoggerService logger = null)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _logger = logger;
            _clipboardItems = new ObservableCollection<ClipboardItem>();

            InitializeCommands();
            InitializeServices();

            _logger?.LogInfo("ViewModel初始化完成");

            // 异步加载历史记录
            _ = LoadHistoryAsync();
        }
        
        #region 属性
        
        /// <summary>
        /// 剪贴板历史记录集合
        /// </summary>
        public ObservableCollection<ClipboardItem> ClipboardItems
        {
            get => _clipboardItems;
            set
            {
                _clipboardItems = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// 当前选中的剪贴板项目
        /// </summary>
        public ClipboardItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                
                // 更新相关命令的可执行状态
                ((RelayCommand)DoubleClickItemCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteItemCommand).RaiseCanExecuteChanged();
            }
        }
        
        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                
                // 执行搜索
                _ = SearchAsync();
            }
        }
        
        /// <summary>
        /// 是否正在监听剪贴板
        /// </summary>
        public bool IsListening
        {
            get => _isListening;
            set
            {
                _isListening = value;
                OnPropertyChanged();
                
                // 更新命令的可执行状态
                ((RelayCommand)StartListeningCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopListeningCommand).RaiseCanExecuteChanged();
            }
        }
        
        /// <summary>
        /// 历史记录总数
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
            }
        }
        
        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
        
        #endregion
        
        #region 命令
        
        /// <summary>
        /// 开始监听命令
        /// </summary>
        public ICommand StartListeningCommand { get; private set; }
        
        /// <summary>
        /// 停止监听命令
        /// </summary>
        public ICommand StopListeningCommand { get; private set; }
        
        /// <summary>
        /// 双击项目命令
        /// </summary>
        public ICommand DoubleClickItemCommand { get; private set; }
        
        /// <summary>
        /// 删除项目命令
        /// </summary>
        public ICommand DeleteItemCommand { get; private set; }
        
        #endregion

        /// <summary>
        /// 记录当前活动窗口
        /// </summary>
        public void RecordPreviousActiveWindow()
        {
            _previousActiveWindow = GetForegroundWindow();
        }
        
        /// <summary>
        /// 处理删除历史记录项事件
        /// </summary>
        private async Task OnDeleteItem()
        {
            if (SelectedItem == null)
            {
                _logger?.LogWarning("尝试删除项目但SelectedItem为null");
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string contentPreview = SelectedItem.Content.Length > 50
                    ? SelectedItem.Content.Substring(0, 50) + "..."
                    : SelectedItem.Content;

                // 格式化删除内容用于日志记录
                string deleteContentPreview = FormatContentForLog(SelectedItem.Content, GetContentType(SelectedItem.Content));
                _logger?.LogUserAction("尝试删除记录", $"ID: {SelectedItem.Id}, 内容: {deleteContentPreview}");

                // 显示确认对话框
                var result = MessageBox.Show(
                    $"确定要删除这条记录吗？\n\n内容预览：{contentPreview}",
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                );

                if (result == MessageBoxResult.Yes)
                {
                    // 在清空SelectedItem之前保存ID
                    int itemId = SelectedItem.Id;

                    // 执行删除操作
                    var success = await _historyService.DeleteItemAsync(itemId);

                    if (success)
                    {
                        // 从集合中移除项目
                        ClipboardItems.Remove(SelectedItem);

                        // 更新总数
                        TotalCount = await _historyService.GetTotalCountAsync();

                        // 清空选中项
                        SelectedItem = null;

                        StatusMessage = "记录已删除";
                        _logger?.LogInfo($"记录删除成功，ID: {itemId}, 内容: {deleteContentPreview}");
                    }
                    else
                    {
                        StatusMessage = "删除失败";
                        _logger?.LogError($"记录删除失败，ID: {itemId}");
                        MessageBox.Show("删除记录失败，请重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    int itemId = SelectedItem.Id;
                    _logger?.LogUserAction("用户取消删除操作", $"ID: {itemId}, 内容: {deleteContentPreview}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除记录时发生错误");
                StatusMessage = $"删除失败: {ex.Message}";
                MessageBox.Show($"删除记录时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                _logger?.LogPerformance("删除记录操作", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// 格式化内容用于日志记录
        /// </summary>
        /// <param name="content">原始内容</param>
        /// <param name="contentType">内容类型</param>
        /// <returns>格式化后的内容</returns>
        private string FormatContentForLog(string content, string contentType)
        {
            if (string.IsNullOrEmpty(content))
                return "[空内容]";

            switch (contentType)
            {
                case "Image":
                    return $"[图片] Base64长度: {content.Length} 字符";

                case "Files":
                    var filePaths = content.Split(';');
                    return $"[文件] {string.Join(", ", filePaths.Take(3))}{(filePaths.Length > 3 ? $" 等{filePaths.Length}个文件" : "")}";

                default: // Text
                    const int maxPreviewLength = 200;
                    string preview = content.Length > maxPreviewLength
                        ? content.Substring(0, maxPreviewLength) + "..."
                        : content;

                    // 处理特殊字符，便于日志阅读
                    preview = preview.Replace("\r\n", "\\r\\n")
                                 .Replace("\n", "\\n")
                                 .Replace("\t", "\\t");

                    return $"[文本] {preview}";
            }
        }

        /// <summary>
        /// 获取内容类型
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns>内容类型</returns>
        private string GetContentType(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "Unknown";

            if (content.StartsWith("iVBORw0KGgo"))
                return "Image";

            if (content.Contains(";"))
                return "Files";

            return "Text";
        }

        /// <summary>
        /// 处理双击历史记录项事件
        /// </summary>
        private async void OnDoubleClickItem()
        {
            if (SelectedItem == null)
            {
                _logger?.LogWarning("尝试双击项目但SelectedItem为null");
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string contentPreview = SelectedItem.Content.Length > 50
                    ? SelectedItem.Content.Substring(0, 50) + "..."
                    : SelectedItem.Content;

                _logger?.LogUserAction("双击历史记录项", $"ID: {SelectedItem.Id}, 内容预览: {contentPreview}");

                // 1. 将选中项的内容设置到剪贴板
                Clipboard.SetText(SelectedItem.Content);
                _logger?.LogClipboardOperation("复制到剪贴板", "Text", SelectedItem.Content.Length);
                StatusMessage = "内容已复制，正在切换窗口并粘贴...";

                // 2. 最小化并隐藏主界面
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = WindowState.Minimized;
                        mainWindow.Hide();
                    }
                });

                // 确保UI更新完成
                await Task.Delay(30);

                // 3. 切换到之前记录的活动窗口
                SwitchToPreviousWindow();

                // 4. 等待窗口切换完成并获得焦点（优化延迟时间）
                await Task.Delay(100);

                // 5. 发送粘贴命令
                await SendCtrlV();
                _logger?.LogDebug("已发送Ctrl+V粘贴命令");
                StatusMessage = "内容已自动粘贴";

                _logger?.LogInfo("双击操作完成，内容已粘贴到目标窗口");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "双击操作过程中发生错误");
                StatusMessage = $"操作失败: {ex.Message}";

                // 失败时恢复窗口
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                });
            }
            finally
            {
                stopwatch.Stop();
                _logger?.LogPerformance("双击操作", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// 发送Ctrl+V按键到当前活动窗口
        /// </summary>
        private Task SendCtrlV()
        {
            try
            {
                INPUT[] inputs = new INPUT[4];
                var extraInfo = GetMessageExtraInfo();

                // Press Ctrl, V, Release V, Release Ctrl
                inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = 0, dwExtraInfo = extraInfo } } };
            inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_V, dwFlags = 0, dwExtraInfo = extraInfo } } };
            inputs[2] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = extraInfo } } };
            inputs[3] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = extraInfo } } };

                SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
            }
            catch
            {
                StatusMessage = "自动粘贴失败，请手动按 Ctrl+V 粘贴";
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 切换到之前记录的活动窗口
        /// </summary>
        private void SwitchToPreviousWindow()
        {
            if (_previousActiveWindow != IntPtr.Zero)
            {
                // 直接激活之前记录的窗口
                SetForegroundWindow(_previousActiveWindow);
            }
            else
            {
                // 如果没有记录的窗口，则使用Alt+Tab切换（简化版本）
                INPUT[] inputs = new INPUT[4];
                
                // 按下 Alt 键
                inputs[0] = new INPUT
                {
                    type = 1, // INPUT_KEYBOARD
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0x12, // VK_MENU (Alt)
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 按下 Tab 键
                inputs[1] = new INPUT
                {
                    type = 1, // INPUT_KEYBOARD
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0x09, // VK_TAB
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 释放 Tab 键
                inputs[2] = new INPUT
                {
                    type = 1, // INPUT_KEYBOARD
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0x09, // VK_TAB
                            wScan = 0,
                            dwFlags = 2, // KEYEVENTF_KEYUP
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 释放 Alt 键
                inputs[3] = new INPUT
                {
                    type = 1, // INPUT_KEYBOARD
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0x12, // VK_MENU (Alt)
                            wScan = 0,
                            dwFlags = 2, // KEYEVENTF_KEYUP
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        #region Windows API

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// INPUT结构体，用于SendInput API
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        /// <summary>
        /// INPUT联合体
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        /// <summary>
        /// 键盘输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 鼠标输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 硬件输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const int INPUT_KEYBOARD = 1;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_V = 0x56;
        private const ushort VK_MENU = 0x12; // Alt key
        private const ushort VK_TAB = 0x09;  // Tab key
        private const uint KEYEVENTF_KEYUP = 0x02;

        #endregion

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            StartListeningCommand = new RelayCommand(
                executeAsync: async () => await StartListeningAsync(),
                canExecute: () => !IsListening
            );
            
            StopListeningCommand = new RelayCommand(
                executeAsync: async () => await StopListeningAsync(),
                canExecute: () => IsListening
            );
            
            DoubleClickItemCommand = new RelayCommand(
                execute: () => OnDoubleClickItem(),
                canExecute: () => SelectedItem != null
            );
            
            DeleteItemCommand = new RelayCommand(
                executeAsync: async () => await OnDeleteItem(),
                canExecute: () => SelectedItem != null
            );
        }
        
        /// <summary>
        /// 初始化服务
        /// </summary>
        private void InitializeServices()
        {
            // 订阅剪贴板变化事件
            _clipboardService.ClipboardChanged += OnClipboardChanged;
        }
        
        /// <summary>
        /// 剪贴板内容变化事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrWhiteSpace(e.ClipboardItem.Content))
                {
                    _logger?.LogDebug("剪贴板内容为空，跳过处理");
                    return;
                }

                string contentType = "Text";
                if (e.ClipboardItem.Content.StartsWith("iVBORw0KGgo")) contentType = "Image";
                else if (e.ClipboardItem.Content.Contains(";")) contentType = "Files";

                // 格式化剪贴板内容用于日志记录
                string contentPreview = FormatContentForLog(e.ClipboardItem.Content, contentType);
                _logger?.LogClipboardOperation("检测到变化", contentType, e.ClipboardItem.Content.Length, contentPreview);

                // 保存到数据库
                var addedItem = await _historyService.AddItemAsync(e.ClipboardItem);
                _logger?.LogDebug("剪贴板内容已保存到数据库");

                // 在UI线程中直接添加新项目到界面
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (addedItem != null)
                    {
                        // 将新项目添加到列表顶部
                        ClipboardItems.Insert(0, addedItem);

                        // 限制显示数量，避免界面过于臃肿
                        const int maxDisplayItems = 1000;
                        while (ClipboardItems.Count > maxDisplayItems)
                        {
                            ClipboardItems.RemoveAt(ClipboardItems.Count - 1);
                        }

                        // 更新总数
                        TotalCount += 1;

                        StatusMessage = "已添加新项目";
                        _logger?.LogDebug($"新项目已添加到界面，当前总数: {TotalCount}");
                    }
                    else
                    {
                        StatusMessage = "添加项目失败";
                        _logger?.LogWarning("AddItemAsync返回null，可能存在重复内容或其他问题");
                    }
                });

                _logger?.LogUserAction("剪贴板内容自动添加", $"类型: {contentType}, 长度: {e.ClipboardItem.Content.Length}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理剪贴板变化时发生错误");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"添加项目失败: {ex.Message}";
                });
            }
            finally
            {
                stopwatch.Stop();
                _logger?.LogPerformance("剪贴板变化处理", stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// 开始监听剪贴板
        /// </summary>
        public async Task StartListeningAsync()
        {
            try
            {
                _clipboardService.StartListening();
                IsListening = true;
                StatusMessage = "正在监听剪贴板...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"启动监听失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 停止监听剪贴板
        /// </summary>
        private async Task StopListeningAsync()
        {
            try
            {
                _clipboardService.StopListening();
                IsListening = false;
                StatusMessage = "已停止监听剪贴板";
            }
            catch (Exception ex)
            {
                StatusMessage = $"停止监听失败: {ex.Message}";
            }
        }
        

        

        

        
        /// <summary>
        /// 加载历史记录
        /// </summary>
        public async Task LoadHistoryAsync()
        {
            try
            {
                var items = await _historyService.GetAllItemsAsync(100, 0);
                var totalCount = await _historyService.GetTotalCountAsync();
                
                ClipboardItems.Clear();
                foreach (var item in items)
                {
                    ClipboardItems.Add(item);
                }
                
                TotalCount = totalCount;
                StatusMessage = $"已加载 {items.Count} 个项目";
                

            }
            catch (Exception ex)
            {
                StatusMessage = $"加载历史记录失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 搜索历史记录
        /// </summary>
        private async Task SearchAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    await LoadHistoryAsync();
                    return;
                }
                
                var items = await _historyService.SearchItemsAsync(SearchText, 100, 0);
                
                ClipboardItems.Clear();
                foreach (var item in items)
                {
                    ClipboardItems.Add(item);
                }
                
                StatusMessage = $"搜索到 {items.Count} 个匹配项目";
            }
            catch (Exception ex)
            {
                StatusMessage = $"搜索失败: {ex.Message}";
            }
        }
        
        #region INotifyPropertyChanged 实现
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region IDisposable 实现
        
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
                // 停止监听（同步方式）
                if (IsListening)
                {
                    try
                    {
                        _clipboardService.StopListening();
                        IsListening = false;
                        StatusMessage = "已停止监听剪贴板";

                        }
                    catch (Exception ex)
                    {
                        StatusMessage = $"停止监听失败: {ex.Message}";
                    }
                }
                
                // 取消订阅事件
                _clipboardService.ClipboardChanged -= OnClipboardChanged;
                
                // 释放服务
                _clipboardService?.Dispose();
                _historyService?.Dispose();
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~MainWindowViewModel()
        {
            Dispose(false);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 简单的命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly bool _isAsync;
        
        /// <summary>
        /// 构造函数（同步版本）
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
            _isAsync = false;
        }
        
        /// <summary>
        /// 构造函数（异步版本）
        /// </summary>
        /// <param name="executeAsync">异步执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute ?? (() => true);
            _isAsync = true;
        }
        
        /// <summary>
        /// 可执行状态变化事件
        /// </summary>
        public event EventHandler? CanExecuteChanged;
        
        /// <summary>
        /// 判断命令是否可执行
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute();
        }
        
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">参数</param>
        public void Execute(object? parameter)
        {
            if (_isAsync)
            {
                _ = _executeAsync();
            }
            else
            {
                _execute();
            }
        }
        
        /// <summary>
        /// 触发可执行状态变化事件
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}