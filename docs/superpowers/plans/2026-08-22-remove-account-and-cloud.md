# 移除账户登录与云端功能 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 移除 PasteList 中的个人账户登录（注册/登录/注销）、剪贴板上传/同步、服务器地址等所有云端相关功能，使其成为纯本地运行的剪贴板历史记录应用。

**Architecture:** 采用「自底向上」的删除式重构，每个任务结束时 `dotnet build` 都能通过：先从视图/视图模型移除上传、账户、同步三条功能链路，再删除已无引用的服务类（`AuthService`、`SettingsService` 等）与转换器，最后删除云端后端（`Server/` 目录）与云端相关文档。本地 SQLite 存储、剪贴板监听、历史记录 CRUD、双击粘贴、托盘、全局热键、开机启动等功能全部保留。

**Tech Stack:** .NET 8.0, WPF, MVVM, Entity Framework Core 9.0.8 + SQLite（Node.js/Express 后端将被整体删除）

**说明（无测试项目）:** 本仓库无测试项目（无 `dotnet test` 可用），验证方式为「每步 `dotnet build` 编译通过 + 最后的手动验收清单」。删除类重构不适用 TDD 红绿循环，故每个任务以「编译通过」作为完成判据。

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `ViewModels/MainWindowViewModel.cs` | 修改 | 移除上传命令、同步逻辑、auth/settings 依赖，简化删除逻辑 |
| `ViewModels/SettingsViewModel.cs` | 重写 | 仅保留开机启动设置 + 保存/取消命令 |
| `MainWindow.xaml` | 修改 | 移除右键菜单「上传」项 |
| `MainWindow.xaml.cs` | 修改 | 移除 AuthService/SettingsService 的创建与注入、自动登录、同步完成处理 |
| `SettingsWindow.xaml` | 重写 | 移除「用户账户」与「高级（同步）」区块，仅保留开机启动 |
| `SettingsWindow.xaml.cs` | 重写 | 构造函数改为只依赖 StartupService；移除密码框/同步事件处理 |
| `Services/AuthService.cs` | 删除 | 云端认证/上传/同步客户端实现 |
| `Services/IAuthService.cs` | 删除 | 认证接口与云端模型（AuthResult/UserInfo/SyncResult 等） |
| `Services/SettingsService.cs` | 删除 | 仅含「同步开关 + 服务器地址」的设置服务，移除后无剩余职责 |
| `Services/ISettingsService.cs` | 删除 | 设置服务接口 |
| `Converters/InverseBoolConverter.cs` | 删除 | 仅被账户登录 UI 的 IsLoading 绑定使用 |
| `Server/`（整个目录） | 删除 | Node.js/Express 云端后端 |
| `Doc/接口文档.md` | 删除 | 云端 HTTP API 接口文档 |
| `Doc/产品需求文档.md` | 修改 | 删除云端/同步/账户相关章节 |
| `Doc/技术规格文档.md` | 修改 | 删除后端/接口/云端相关章节 |

---

### Task 1: 移除「上传」功能（主窗口右键菜单 + ViewModel）

**Files:**
- 修改: `MainWindow.xaml:196-210`
- 修改: `ViewModels/MainWindowViewModel.cs`

**说明:** 先移除最独立的上传链路。上传依赖 `IAuthService.UploadClipboardItemAsync`，移除后 auth/sync 仍保留，编译不受影响。

- [ ] **Step 1: 删除主窗口右键菜单的「上传」项**

将 `MainWindow.xaml` 第 202-204 行的上传菜单项及上方分隔线删除：

```xml
<ContextMenu>
    <MenuItem Header="编辑"
              Command="{Binding EditItemCommand}"
              CommandParameter="{Binding SelectedItem}"/>
    <Separator/>
    <MenuItem Header="删除"
              Command="{Binding DeleteItemCommand}"
              CommandParameter="{Binding SelectedItem}"/>
</ContextMenu>
```

（删除原文中的 `<Separator/>` + `<MenuItem Header="上传" .../>` 一对）

- [ ] **Step 2: 移除 `UploadItemCommand` 属性**

删除 `ViewModels/MainWindowViewModel.cs` 第 198-201 行：

```csharp
        /// <summary>
        /// 上传项目命令
        /// </summary>
        public ICommand UploadItemCommand { get; private set; } = null!;
```

- [ ] **Step 3: 移除 `SelectedItem` 中对上传命令的刷新**

删除第 108 行：

```csharp
                ((RelayCommand)UploadItemCommand).RaiseCanExecuteChanged();
```

- [ ] **Step 4: 删除 `OnUploadItem()` 方法**

删除 `ViewModels/MainWindowViewModel.cs` 第 347-414 行整个 `OnUploadItem()` 方法（含 XML 注释）。

- [ ] **Step 5: 移除 `InitializeCommands()` 中的上传命令注册**

删除第 901-904 行：

```csharp
            UploadItemCommand = new RelayCommand(
                executeAsync: async () => await OnUploadItem(),
                canExecute: () => SelectedItem != null && _authService != null
            );
```

- [ ] **Step 6: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 7: Commit**

```bash
git add MainWindow.xaml ViewModels/MainWindowViewModel.cs
git commit -m "refactor: 移除剪贴板项上传功能"
```

---

### Task 2: 重写设置窗口为纯本地（仅开机启动）

**Files:**
- 重写: `ViewModels/SettingsViewModel.cs`
- 重写: `SettingsWindow.xaml`
- 重写: `SettingsWindow.xaml.cs`
- 修改: `MainWindow.xaml.cs`（SettingsMenuItem_Click 中构造 SettingsWindow 的调用）

**说明:** 设置窗口原本承担「账户登录/注册/注销 + 同步开关」。移除后仅剩「开机自动启动」。本任务结束时 `SettingsViewModel`/`SettingsWindow` 不再依赖 `IAuthService`/`ISettingsService`/`IClipboardHistoryService`；但 `MainWindowViewModel` 仍在使用它们（在 Task 3 才移除），因此编译仍通过。

- [ ] **Step 1: 用完整内容替换 `ViewModels/SettingsViewModel.cs`**

```csharp
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
        private readonly ILoggerService? _logger;
        private bool _isStartupEnabled;
        private bool _hasChanges;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsViewModel(IStartupService startupService, ILoggerService? logger = null)
        {
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            _logger = logger;

            SaveCommand = new RelayCommand(
                executeAsync: SaveAsync,
                canExecute: () => HasChanges
            );

            CancelCommand = new RelayCommand(
                executeAsync: CancelAsync,
                canExecute: () => true
            );
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

        /// <summary>
        /// 加载当前设置
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                _isStartupEnabled = _startupService.IsStartupEnabled();
                HasChanges = false;
                OnPropertyChanged(nameof(IsStartupEnabled));
                _logger?.LogDebug($"设置窗口加载完成，开机启动状态: {(_isStartupEnabled ? "已启用" : "已禁用")}");
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
                return LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取消设置时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

- [ ] **Step 2: 用完整内容替换 `SettingsWindow.xaml`**

```xml
<Window x:Class="PasteList.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="首选项" Height="300" Width="400"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        WindowStyle="SingleBorderWindow"
        ShowInTaskbar="False">
    <Window.Resources>
        <!-- 主要按钮样式 -->
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#4CAF50"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="3"
                                SnapsToDevicePixels="True">
                            <ContentPresenter HorizontalAlignment="Center"
                                            VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#45A049"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter Property="Background" Value="#388E3C"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Background" Value="#E0E0E0"/>
                                <Setter Property="Foreground" Value="#666666"/>
                                <Setter Property="Cursor" Value="Arrow"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- 次要按钮样式 -->
        <Style x:Key="SecondaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#9E9E9E"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Height" Value="32"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="3"
                                SnapsToDevicePixels="True">
                            <ContentPresenter HorizontalAlignment="Center"
                                            VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#757575"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter Property="Background" Value="#616161"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Background" Value="#E0E0E0"/>
                                <Setter Property="Foreground" Value="#666666"/>
                                <Setter Property="Cursor" Value="Arrow"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 常规设置区域 -->
        <Border Grid.Row="0" Padding="15">
            <StackPanel>
                <TextBlock Text="启动" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,10"/>

                <!-- 开机启动设置 -->
                <CheckBox Content="开机自动启动"
                          IsChecked="{Binding IsStartupEnabled}"
                          Margin="10,5,0,0"
                          FontSize="13"/>
            </StackPanel>
        </Border>

        <!-- 按钮区域 -->
        <Border Grid.Row="1" Background="#F5F5F5" Padding="20">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Content="确定" Width="80" Height="30"
                        Click="SaveButton_Click"
                        Margin="0,0,10,0"
                        Style="{StaticResource PrimaryButtonStyle}"/>

                <Button Content="取消" Width="80" Height="30"
                        Click="CancelButton_Click"
                        Style="{StaticResource SecondaryButtonStyle}"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 3: 用完整内容替换 `SettingsWindow.xaml.cs`**

```csharp
using System;
using System.Windows;
using PasteList.Services;
using PasteList.ViewModels;

namespace PasteList
{
    /// <summary>
    /// 设置窗口的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="startupService">启动服务</param>
        /// <param name="logger">日志服务</param>
        public SettingsWindow(IStartupService startupService, ILoggerService? logger)
        {
            InitializeComponent();

            _viewModel = new SettingsViewModel(startupService, logger);
            DataContext = _viewModel;

            this.Loaded += Window_Loaded;
        }

        /// <summary>
        /// 窗口加载完成后的事件处理
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// 处理确定按钮点击事件
        /// </summary>
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel.SaveCommand is RelayCommand relayCommand)
                {
                    await relayCommand.ExecuteAsync(null);
                }
                else
                {
                    _viewModel.SaveCommand.Execute(null);
                    await Task.Delay(100);
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.CancelCommand.Execute(null);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
```

- [ ] **Step 4: 更新 `MainWindow.xaml.cs` 中打开设置窗口的逻辑**

在 `SettingsMenuItem_Click` 中：

1. 删除 `_authService == null` 检查块（原第 713-717 行）。
2. 删除 `_historyService == null` 检查块（原第 719-723 行）。
3. 删除 `_settingsService == null` 检查块（原第 725-729 行）。
4. 将第 732 行的构造调用替换为：

```csharp
                // 创建设置窗口
                var settingsWindow = new SettingsWindow(_startupService, _logger);
                settingsWindow.Owner = this;

                // 显示设置窗口（模态对话框）
                settingsWindow.ShowDialog();
```

（同时删除原第 735-743 行的 `SyncCompleted` 订阅块）

- [ ] **Step 5: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误（此时 `MainWindowViewModel` 仍引用 auth/settings，故相关服务文件尚未删除）。

- [ ] **Step 6: Commit**

```bash
git add ViewModels/SettingsViewModel.cs SettingsWindow.xaml SettingsWindow.xaml.cs MainWindow.xaml.cs
git commit -m "refactor: 设置窗口移除账户与同步，仅保留开机启动设置"
```

---

### Task 3: 移除主窗口的同步与账户依赖

**Files:**
- 修改: `ViewModels/MainWindowViewModel.cs`
- 修改: `MainWindow.xaml.cs`

**说明:** 移除 `MainWindowViewModel` 中的 `_authService`/`_settingsService` 依赖、`SyncFromServerAsync()`、删除逻辑中的「服务器同步删除」分支，以及剪贴板变化中的同步触发；同时更新 `MainWindow.xaml.cs` 的启动初始化，停止创建 `AuthService`/`SettingsService` 并停止自动登录。

- [ ] **Step 1: 移除字段与构造函数参数**

删除 `ViewModels/MainWindowViewModel.cs` 第 23-24 行字段：

```csharp
        private readonly IAuthService? _authService;
        private readonly ISettingsService? _settingsService;
```

将构造函数签名与赋值（第 59-67 行）改为：

```csharp
        /// <summary>
        /// 带参数的构造函数（用于运行时）
        /// </summary>
        /// <param name="clipboardService">剪贴板服务</param>
        /// <param name="historyService">历史记录服务</param>
        /// <param name="logger">日志服务</param>
        public MainWindowViewModel(IClipboardService clipboardService, IClipboardHistoryService historyService, ILoggerService? logger = null)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _logger = logger;
            _clipboardItems = new ObservableCollection<ClipboardItem>();
```

> 注：以上仅替换构造函数的签名与字段赋值部分；原构造函数中后续的 `InitializeCommands();`、`InitializeServices();`、`_logger?.LogInfo("ViewModel初始化完成");`、`_ = LoadHistoryAsync();` 与收尾 `}` 保持不变，不要整体删除构造函数。

- [ ] **Step 2: 用纯本地版本替换 `OnDeleteItem()`**

将第 226-345 行的 `OnDeleteItem()` 整个方法替换为：

```csharp
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

                string deleteContentPreview = FormatContentForLog(SelectedItem.Content, GetContentType(SelectedItem.Content));
                _logger?.LogUserAction("尝试删除记录", $"ID: {SelectedItem.Id}, 内容: {deleteContentPreview}");

                string confirmMessage = $"确定要删除这条记录吗？\n\n内容预览：{contentPreview}";

                // 显示确认对话框
                var result = MessageBox.Show(
                    confirmMessage,
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                );

                if (result == MessageBoxResult.Yes)
                {
                    int itemId = SelectedItem.Id;

                    StatusMessage = "正在删除本地记录...";

                    var success = await _historyService.DeleteItemAsync(itemId);

                    if (success)
                    {
                        ClipboardItems.Remove(SelectedItem);
                        TotalCount = await _historyService.GetTotalCountAsync();
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
```

- [ ] **Step 3: 删除 `SyncFromServerAsync()` 方法**

删除第 1093-1155 行的整个 `SyncFromServerAsync()` 方法（含 XML 注释）。

- [ ] **Step 4: 移除剪贴板变化处理中的同步触发**

删除 `OnClipboardChanged()` 第 985-986 行：

```csharp
                // 如果启用了同步，触发从服务器同步
                await SyncFromServerAsync();
```

- [ ] **Step 5: 更新 `MainWindow.xaml.cs` 的启动初始化**

在 `InitializeAsync()` 中：

1. 删除第 184-210 行（设置服务、认证服务初始化与自动登录块），即以下内容全部删除：

```csharp
            // 初始化设置服务（需要在认证服务之前，因为认证服务需要读取服务器地址）
            _settingsService = new SettingsService(_logger);
            await _settingsService.LoadAsync();
            _logger.LogInfo("设置服务初始化完成");

            // 初始化认证服务
            _authService = new AuthService(_settingsService, _logger);
            _logger.LogInfo("认证服务初始化完成");

            // 尝试自动登录（使用保存的凭证）
            try
            {
                var autoLoginSuccess = await _authService.TryAutoLoginAsync();
                if (autoLoginSuccess)
                {
                    _logger.LogInfo("自动登录成功");
                }
                else
                {
                    _logger.LogDebug("自动登录失败或没有保存的凭证");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动登录初始化失败");
                // 不阻断应用启动，继续运行
            }
```

2. 将第 213 行替换为：

```csharp
            _viewModel = new MainWindowViewModel(_clipboardService, _historyService, _logger);
```

- [ ] **Step 6: 移除 `MainWindow.xaml.cs` 中的 auth/settings 字段**

删除第 110-111 行：

```csharp
        private IAuthService? _authService;
        private ISettingsService? _settingsService;
```

- [ ] **Step 7: 构建验证编译**

```bash
dotnet build
```
预期：编译通过（`AuthService`/`SettingsService` 文件虽仍存在但已无引用）。

- [ ] **Step 8: Commit**

```bash
git add ViewModels/MainWindowViewModel.cs MainWindow.xaml.cs
git commit -m "refactor: 移除主窗口的账户与同步逻辑，纯本地运行"
```

---

### Task 4: 删除已无引用的服务与转换器文件

**Files:**
- 删除: `Services/AuthService.cs`
- 删除: `Services/IAuthService.cs`
- 删除: `Services/SettingsService.cs`
- 删除: `Services/ISettingsService.cs`
- 删除: `Converters/InverseBoolConverter.cs`

**说明:** 这些文件在上面的任务后已无任何引用。删除后需确认编译仍通过（不存在其他隐式引用）。

- [ ] **Step 1: 确认无残留引用**

```bash
# 使用 grep 检查（在仓库根目录执行）
```
在 `*.cs` 与 `*.xaml` 中搜索 `AuthService`、`IAuthService`、`SettingsService`、`ISettingsService`、`InverseBoolConverter`，应仅在待删除文件自身中出现。

- [ ] **Step 2: 删除五个文件**

```bash
git rm Services/AuthService.cs Services/IAuthService.cs Services/SettingsService.cs Services/ISettingsService.cs Converters/InverseBoolConverter.cs
```

- [ ] **Step 3: 构建验证编译**

```bash
dotnet build
```
预期：编译通过，无错误。

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor: 删除无引用的认证/设置服务与转换器"
```

---

### Task 5: 删除云端后端与云端接口文档

**Files:**
- 删除: `Server/`（整个目录，含 `package.json`、`src/`、`database/`、`.env*` 等）
- 删除: `Doc/接口文档.md`

**说明:** `Server/` 是 Node.js/Express 云端后端（认证、剪贴板、设备、用户接口），是「云端功能」的承载端；`Doc/接口文档.md` 是其 HTTP API 文档。两者都应随云端功能一并移除。

- [ ] **Step 1: 删除 Server 目录与接口文档**

`Server/` 内含有被忽略（未跟踪）的 `node_modules` 等目录，`git rm -r` 可能报错或留下残留，因此先在文件系统层面彻底删除目录，再清理 git 跟踪：

```powershell
# 1) 彻底删除 Server 目录（含 node_modules 等未跟踪内容）
Remove-Item -Path .\Server -Recurse -Force

# 2) 从 git 索引移除仍被跟踪的 Server 文件（若已无跟踪文件则此步为 no-op）
git rm -r --ignore-unmatch Server

# 3) 删除云端接口文档
git rm "Doc/接口文档.md"
```

- [ ] **Step 2: 确认 .sln 未引用 Server（应无影响）**

`PasteList.sln` 仅引用 `PasteList.csproj`，无需修改。确认 `.gitignore` 无需为 Server 增删条目（原 `.gitignore` 仅含 `/bin`、`/obj`、`/.vs`、`.cursor/debug.log`，均与 Server 无关）。

- [ ] **Step 3: Commit**

```bash
git commit -m "refactor: 删除云端后端与云端接口文档"
```

---

### Task 6: 清理云端相关文档章节

**Files:**
- 修改: `Doc/产品需求文档.md`
- 修改: `Doc/技术规格文档.md`

**说明:** 两篇文档仍含有账户/同步/后端/接口相关章节，与「纯本地应用」定位不一致。删除相应章节，使文档与代码一致。

- [ ] **Step 1: 清理 `Doc/产品需求文档.md`**

删除或改写以下内容：
- 第 18 行：将「快速搜索和跨设备同步的需求」改为「快速搜索的需求」。
- 第 84-91 行：删除「### 2.6 跨设备同步」整节。
- 第 100 行：删除 2.7 表中的「同步设置」行。
- 第 119-122 行：删除 3.2 安全性需求表中的「网络安全」「认证安全」「密码安全」三行；将「隐私保护」描述改为「本地数据不自动上传」。
- 第 164、166 行：删除 4.2 表中「多设备用户」行；改写「注重隐私用户」行为仅本地过滤。
- 第 174-175 行：删除「设备信息（同步时）」「同步状态」字样。
- 第 187 行：删除 5.3 中的「同步设置」行。
- 第 214-247 行：删除「### 6.2 云端数据模型」整节。
- 第 255 行：将「SQLite（本地）、MongoDB/MySQL（云端）」改为「SQLite（本地）」。
- 第 260-262 行：删除 7.2 中的「服务端」「通信」「认证」三条。
- 第 277 行：删除 7.4 中的「采用增量同步机制，减少网络传输数据量」。
- 第 292 行：删除 8.1 表中的「数据同步」行。
- 第 308 行：将「容错性」的「网络中断后恢复，能正常重新同步」改为「磁盘空间不足等异常有良好容错处理」。
- 第 326 行：删除 9 表中的「网络同步问题」行。
- 第 335、337 行：删除 10.1 术语表中的「JWT」「RESTful API」行。
- 第 344 行：删除 10.2 中的「JSON Web Token 官方文档」链接。

- [ ] **Step 2: 清理 `Doc/技术规格文档.md`**

删除或改写以下内容：
- 第 10、12 行：将「桌面应用程序 + 后端服务」改为「桌面应用程序」；开发技术去掉「Node.js + Express」。
- 第 33 行：删除 2.3 中的「跨设备数据同步」。
- 第 67-90 行：将 3.1 架构图简化为仅「客户端应用 → 数据访问层(EF Core) → SQLite」，删除「网络通信层 / 后端服务(Node.js) / 云端数据库」三层。
- 第 99-103 行：删除「### 3.3 服务端架构」整节。
- 第 118-129 行：删除「### 4.2 服务端技术栈」整节。
- 第 155-200 行：删除「### 5.2 云端数据模型」整节。
- 第 202-247 行：删除「## 6. 接口定义」整节（后续章节编号顺延或保持，仅内容删除）。
- 第 258-265 行：删除「### 7.2 服务端安全」整节。
- 第 286-293 行：删除「### 8.2 服务端性能」整节。
- 第 326-358 行：删除「### 9.2 服务端部署」整节。
- 第 395 行：删除 11.2 中的「服务端监控：API 响应时间、错误率、并发数、资源使用率」。
- 第 412、414、416 行：删除 12.1 术语表中的「JWT」「RESTful API」「TLS」行。
- 第 424-425 行：删除 12.2 中的「JSON Web Token」「Express」链接。

- [ ] **Step 3: Commit**

```bash
git add "Doc/产品需求文档.md" "Doc/技术规格文档.md"
git commit -m "docs: 移除需求与技术文档中的云端/账户相关章节"
```

---

### Task 7: 端到端构建与验收

**Files:**
- 无需修改

**说明:** 全量构建并手动验收，确认应用为纯本地运行且核心功能不受影响。

- [ ] **Step 1: 全量构建**

```bash
dotnet build -c Release
```
预期：编译通过，无错误、无警告（关于已删除类型的错误）。

- [ ] **Step 2: 手动验收清单**

1. 启动应用 → 状态栏显示「应用程序已就绪」，自动开启监听。
2. 复制文本 → 历史列表出现新条目（本地写入，无网络请求）。
3. 右键历史项 → 菜单仅含「编辑」「删除」，无「上传」。
4. 删除历史项 → 确认框仅提示本地删除，无「同时删除服务器记录」字样，删除成功。
5. 双击历史项 → 正常粘贴到之前活动窗口。
6. 打开「首选项」→ 仅显示「开机自动启动」复选框与「确定/取消」，无账户、无同步、无「高级」标签页。
7. 勾选/取消开机启动 → 保存后生效（任务计划/注册表）。
8. 检查程序目录下不再生成 `auth.json`、`settings.json`。
9. 检查日志 `Logs/PasteList-{Date}.log` → 无网络请求、无认证、无同步相关日志。

- [ ] **Step 3: 确认 `Server/` 与云端文档已不在工作区**

```bash
git status
```
预期：`Server/`、`Doc/接口文档.md` 已删除，无未跟踪的云端相关文件。

- [ ] **Step 4: 最终 Commit（如有遗漏的文档/注释调整）**

```bash
git add -A
git commit -m "chore: 完成纯本地化改造的验证"
```
