using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        /// 开始监听剪贴板命令
        /// </summary>
        public ICommand StartListeningCommand { get; private set; } = null!;
        
        /// <summary>
        /// 停止监听剪贴板命令
        /// </summary>
        public ICommand StopListeningCommand { get; private set; } = null!;
        

        
        #endregion
        
        #region 私有方法
        
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
        
        #endregion
        
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