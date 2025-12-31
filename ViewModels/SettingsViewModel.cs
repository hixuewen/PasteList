using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PasteList.Services;

namespace PasteList.ViewModels
{
    /// <summary>
    /// 设置窗口的ViewModel
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IStartupService _startupService;
        private readonly IAuthService _authService;
        private readonly ILoggerService? _logger;
        private bool _isStartupEnabled;
        private bool _hasChanges;

        // 认证相关字段
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private bool _isRegisterMode = false;
        private bool _isLoading = false;
        private string _authStatusMessage = string.Empty;
        private bool _isLoggedIn = false;
        private string _currentUsername = string.Empty;
        private string _currentEmail = string.Empty;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="authService">认证服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsViewModel(IStartupService startupService, IAuthService authService, ILoggerService? logger = null)
        {
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger;

            // 初始化命令
            SaveCommand = new RelayCommand(
                executeAsync: SaveAsync,
                canExecute: () => HasChanges
            );

            CancelCommand = new RelayCommand(
                executeAsync: CancelAsync,
                canExecute: () => true
            );

            // 初始化认证相关命令
            LoginCommand = new RelayCommand(
                executeAsync: LoginAsync,
                canExecute: () => !IsLoading && !IsLoggedIn && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password)
            );

            RegisterCommand = new RelayCommand(
                executeAsync: RegisterAsync,
                canExecute: () => !IsLoading && !IsLoggedIn && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password) && Password == ConfirmPassword
            );

            LogoutCommand = new RelayCommand(
                executeAsync: LogoutAsync,
                canExecute: () => !IsLoading && IsLoggedIn
            );

            ToggleModeCommand = new RelayCommand(
                execute: () => IsRegisterMode = !IsRegisterMode,
                canExecute: () => !IsLoading && !IsLoggedIn
            );

            // 订阅登录状态变化事件
            _authService.LoginStateChanged += OnLoginStateChanged;

            // 初始化登录状态
            UpdateLoginState();
        }

        /// <summary>
        /// 是否启用开机启动
        /// </summary>
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (_isStartupEnabled != value)
                {
                    _isStartupEnabled = value;
                    HasChanges = true;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否有未保存的更改
        /// </summary>
        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                _hasChanges = value;
                OnPropertyChanged();
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 保存命令
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        #region 认证相关属性

        /// <summary>
        /// 用户名（登录/注册时输入）
        /// </summary>
        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 邮箱（注册时输入）
        /// </summary>
        public string Email
        {
            get => _email;
            set
            {
                if (_email != value)
                {
                    _email = value;
                    OnPropertyChanged();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 确认密码（注册时输入）
        /// </summary>
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (_confirmPassword != value)
                {
                    _confirmPassword = value;
                    OnPropertyChanged();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否为注册模式（false为登录模式）
        /// </summary>
        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set
            {
                if (_isRegisterMode != value)
                {
                    _isRegisterMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLoginMode));
                    OnPropertyChanged(nameof(AuthModeButtonText));
                    OnPropertyChanged(nameof(AuthModeSwitchText));
                    ClearAuthInputs();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否为登录模式
        /// </summary>
        public bool IsLoginMode => !IsRegisterMode;

        /// <summary>
        /// 认证模式按钮文本
        /// </summary>
        public string AuthModeButtonText => IsRegisterMode ? "注册" : "登录";

        /// <summary>
        /// 认证模式切换提示文本
        /// </summary>
        public string AuthModeSwitchText => IsRegisterMode ? "已有账号？去登录" : "没有账号？去注册";

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 认证状态消息
        /// </summary>
        public string AuthStatusMessage
        {
            get => _authStatusMessage;
            set
            {
                if (_authStatusMessage != value)
                {
                    _authStatusMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAuthStatusMessage));
                }
            }
        }

        /// <summary>
        /// 是否有认证状态消息
        /// </summary>
        public bool HasAuthStatusMessage => !string.IsNullOrEmpty(AuthStatusMessage);

        /// <summary>
        /// 是否已登录
        /// </summary>
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            private set
            {
                if (_isLoggedIn != value)
                {
                    _isLoggedIn = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotLoggedIn));
                    RaiseAuthCommandsCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 是否未登录
        /// </summary>
        public bool IsNotLoggedIn => !IsLoggedIn;

        /// <summary>
        /// 当前登录用户名
        /// </summary>
        public string CurrentUsername
        {
            get => _currentUsername;
            private set
            {
                if (_currentUsername != value)
                {
                    _currentUsername = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 当前登录用户邮箱
        /// </summary>
        public string CurrentEmail
        {
            get => _currentEmail;
            private set
            {
                if (_currentEmail != value)
                {
                    _currentEmail = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 登录命令
        /// </summary>
        public ICommand LoginCommand { get; }

        /// <summary>
        /// 注册命令
        /// </summary>
        public ICommand RegisterCommand { get; }

        /// <summary>
        /// 注销命令
        /// </summary>
        public ICommand LogoutCommand { get; }

        /// <summary>
        /// 切换登录/注册模式命令
        /// </summary>
        public ICommand ToggleModeCommand { get; }

        #endregion

        /// <summary>
        /// 加载当前设置
        /// </summary>
        public Task LoadSettingsAsync()
        {
            try
            {
                // 加载开机启动设置
                _isStartupEnabled = _startupService.IsStartupEnabled();

                HasChanges = false;
                OnPropertyChanged(nameof(IsStartupEnabled));

                _logger?.LogDebug($"设置窗口加载完成，开机启动状态: {(_isStartupEnabled ? "已启用" : "已禁用")}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载设置时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private Task SaveAsync()
        {
            try
            {
                // 保存开机启动设置
                var oldStartupState = _startupService.IsStartupEnabled();

                if (IsStartupEnabled && !oldStartupState)
                {
                    _startupService.EnableStartup();
                    _logger?.LogUserAction("启用开机启动", "通过设置窗口");
                }
                else if (!IsStartupEnabled && oldStartupState)
                {
                    _startupService.DisableStartup();
                    _logger?.LogUserAction("禁用开机启动", "通过设置窗口");
                }

                HasChanges = false;
                _logger?.LogInfo("设置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存设置时发生错误");
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 取消设置
        /// </summary>
        private Task CancelAsync()
        {
            try
            {
                // 重新加载设置，不保存任何更改
                return LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取消设置时发生错误");
                throw;
            }
        }

        #region 认证相关方法

        /// <summary>
        /// 登录
        /// </summary>
        private async Task LoginAsync()
        {
            try
            {
                IsLoading = true;
                AuthStatusMessage = "正在登录...";

                var result = await _authService.LoginAsync(Username, Password);

                if (result.Success)
                {
                    AuthStatusMessage = "登录成功！";
                    UpdateLoginState();
                    ClearAuthInputs();
                    OnPasswordBoxClearRequested();
                    _logger?.LogUserAction("用户登录成功", $"用户: {Username}");
                }
                else
                {
                    AuthStatusMessage = result.ErrorMessage ?? "登录失败";
                    _logger?.LogWarning($"用户登录失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"登录失败: {ex.Message}";
                _logger?.LogError(ex, "登录过程中发生错误");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 注册
        /// </summary>
        private async Task RegisterAsync()
        {
            try
            {
                // 验证密码确认
                if (Password != ConfirmPassword)
                {
                    AuthStatusMessage = "两次输入的密码不一致";
                    return;
                }

                IsLoading = true;
                AuthStatusMessage = "正在注册...";

                var result = await _authService.RegisterAsync(Username, Email, Password);

                if (result.Success)
                {
                    AuthStatusMessage = "注册成功！";
                    UpdateLoginState();
                    ClearAuthInputs();
                    OnPasswordBoxClearRequested();
                    _logger?.LogUserAction("用户注册成功", $"用户: {Username}");
                }
                else
                {
                    AuthStatusMessage = result.ErrorMessage ?? "注册失败";
                    _logger?.LogWarning($"用户注册失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"注册失败: {ex.Message}";
                _logger?.LogError(ex, "注册过程中发生错误");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 注销
        /// </summary>
        private async Task LogoutAsync()
        {
            try
            {
                IsLoading = true;
                AuthStatusMessage = "正在注销...";

                var username = CurrentUsername;
                await _authService.LogoutAsync();

                UpdateLoginState();
                ClearAuthInputs();
                OnPasswordBoxClearRequested();
                AuthStatusMessage = "已注销";
                _logger?.LogUserAction("用户注销", $"用户: {username}");
            }
            catch (Exception ex)
            {
                AuthStatusMessage = $"注销失败: {ex.Message}";
                _logger?.LogError(ex, "注销过程中发生错误");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 登录状态变化事件处理
        /// </summary>
        private void OnLoginStateChanged(object? sender, bool isLoggedIn)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateLoginState();
            });
        }

        /// <summary>
        /// 更新登录状态
        /// </summary>
        private void UpdateLoginState()
        {
            IsLoggedIn = _authService.IsLoggedIn;
            
            if (_authService.CurrentUser != null)
            {
                CurrentUsername = _authService.CurrentUser.Username;
                CurrentEmail = _authService.CurrentUser.Email;
            }
            else
            {
                CurrentUsername = string.Empty;
                CurrentEmail = string.Empty;
            }
        }

        /// <summary>
        /// 清除认证输入
        /// </summary>
        private void ClearAuthInputs()
        {
            _username = string.Empty;
            _email = string.Empty;
            _password = string.Empty;
            _confirmPassword = string.Empty;
            
            OnPropertyChanged(nameof(Username));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(Password));
            OnPropertyChanged(nameof(ConfirmPassword));
        }

        /// <summary>
        /// 刷新认证相关命令的可执行状态
        /// </summary>
        private void RaiseAuthCommandsCanExecuteChanged()
        {
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LogoutCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ToggleModeCommand).RaiseCanExecuteChanged();
        }

        #endregion

        /// <summary>
        /// 请求清空密码框事件
        /// </summary>
        public event EventHandler? PasswordBoxClearRequested;

        /// <summary>
        /// 触发清空密码框事件
        /// </summary>
        private void OnPasswordBoxClearRequested()
        {
            PasswordBoxClearRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
