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
        public MainWindowViewModel(IClipboardService clipboardService, IClipboardHistoryService historyService)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _clipboardItems = new ObservableCollection<ClipboardItem>();
            
            InitializeCommands();
            InitializeServices();
            
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
        
        #endregion

        /// <summary>
        /// 记录当前活动窗口
        /// </summary>
        public void RecordPreviousActiveWindow()
        {
            _previousActiveWindow = GetForegroundWindow();
        }
        
        /// <summary>
        /// 处理双击历史记录项事件
        /// </summary>
        private async void OnDoubleClickItem()
        {
            if (SelectedItem == null) return;

            try
            {
                // 1. 将选中项的内容设置到剪贴板
                Clipboard.SetText(SelectedItem.Content);
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
                await Task.Delay(50);

                // 3. 模拟 Alt+Tab 切换到上一个窗口
                await SwitchToPreviousWindow();

                // 4. 等待窗口切换完成并获得焦点
                await Task.Delay(300);

                // 5. 发送粘贴命令
                await SendCtrlV();
                StatusMessage = "内容已自动粘贴";
            }
            catch (Exception ex)
            {
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
        private async Task SwitchToPreviousWindow()
        {
            if (_previousActiveWindow != IntPtr.Zero)
            {
                // 直接激活之前记录的窗口
                SetForegroundWindow(_previousActiveWindow);
            }
            else
            {
                // 如果没有记录的窗口，则使用Alt+Tab切换
                INPUT[] inputs = new INPUT[1];
                var extraInfo = GetMessageExtraInfo();

                // Press Alt
                inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_MENU, dwFlags = 0, dwExtraInfo = extraInfo } } };
                SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
                await Task.Delay(50);

                // Press Tab
                inputs[0].u.ki.wVk = VK_TAB;
                inputs[0].u.ki.dwFlags = 0;
                SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
                await Task.Delay(50);

                // Release Tab
                inputs[0].u.ki.wVk = VK_TAB;
                inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
                await Task.Delay(50);
                
                // Release Alt
                inputs[0].u.ki.wVk = VK_MENU;
                inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT)));
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
                execute: async () => await StartListeningAsync(),
                canExecute: () => !IsListening
            );
            
            StopListeningCommand = new RelayCommand(
                execute: () => StopListening(),
                canExecute: () => IsListening
            );
            
            DoubleClickItemCommand = new RelayCommand(
                execute: () => OnDoubleClickItem(),
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
            try
            {
                if (string.IsNullOrWhiteSpace(e.ClipboardItem.Content))
                    return;
                
                // 保存到数据库
                await _historyService.AddItemAsync(e.ClipboardItem);
                
                // 刷新界面
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadHistoryAsync();
                    StatusMessage = "已添加新项目";
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"添加项目失败: {ex.Message}";
                });
            }
        }
        
        /// <summary>
        /// 开始监听剪贴板
        /// </summary>
        private async Task StartListeningAsync()
        {
            try
            {
                _clipboardService.StartListening();
                IsListening = true;
                StatusMessage = "正在监听剪贴板...";
                await Task.CompletedTask; // 保持异步签名
            }
            catch (Exception ex)
            {
                StatusMessage = $"启动监听失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 停止监听剪贴板
        /// </summary>
        private void StopListening()
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
                // 停止监听
                if (IsListening)
                {
                    StopListening();
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
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
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
            _execute();
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