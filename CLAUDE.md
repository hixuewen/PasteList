# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个基于.NET 8.0和WPF的剪贴板历史记录应用程序，采用MVVM架构模式。应用程序支持剪贴板监听、历史记录管理、系统托盘集成、全局快捷键和开机启动等功能。

## 常用开发命令

### 构建和运行
```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行应用程序
dotnet run

# 发布 Release 版本
dotnet publish -c Release -r win-x64 --self-contained
```

### 数据库迁移
```bash
# 创建新的迁移
dotnet ef migrations add [MigrationName] --project PasteList.csproj

# 更新数据库
dotnet ef database update --project PasteList.csproj
```

### 测试和代码质量
```bash
# 运行所有测试（如果有）
dotnet test

# 构建并检查编译错误
dotnet build --no-restore
```

## 架构概览

### 技术栈
- **框架**: .NET 8.0 (Windows Desktop)
- **UI框架**: WPF (Windows Presentation Foundation)
- **数据库**: SQLite + Entity Framework Core 9.0.8
- **架构模式**: MVVM (Model-View-ViewModel)
- **系统托盘**: Hardcodet.NotifyIcon.Wpf 1.1.0

### 核心架构组件

#### 1. 数据层 (Data/)
- **ClipboardDbContext.cs**: Entity Framework数据库上下文
- 数据库文件位置: `%LocalAppData%/PasteList/clipboard.db`
- 使用SQLite作为存储引擎

#### 2. 模型层 (Models/)
- **ClipboardItem.cs**: 剪贴板项目数据实体
- 支持文本、图片(Base64)、文件路径等多种格式
- 使用Entity Framework数据注解

#### 3. 服务层 (Services/)
- **IClipboardService/ClipboardService**: 剪贴板监听服务
  - 使用Windows API (AddClipboardFormatListener) 实时监听剪贴板变化
  - 支持多种格式：文本、图片(Base64)、文件路径列表
  - 智能防重复机制，避免相同内容重复记录
  - 窗口消息钩子处理机制
  - 实现IDisposable接口，正确管理资源

- **IClipboardHistoryService/ClipboardHistoryService**: 历史记录管理服务
  - 异步CRUD操作，避免阻塞UI
  - 分页查询支持，提升大数据量性能
  - 实时搜索功能，支持模糊匹配
  - 内置防重复检查机制

- **IStartupService/StartupService**: 开机启动管理服务
  - 通过Windows注册表实现开机自启动
  - 路径验证和安全性检查
  - 权限检查和错误处理

- **ILoggerService/LoggerService**: 结构化日志服务
  - 基于Serilog的多级别日志系统
  - 支持剪贴板操作专项日志记录
  - 性能监控和用户操作审计
  - 异步日志记录，避免阻塞主线程

#### 4. 视图模型层 (ViewModels/)
- **MainWindowViewModel.cs**: 主窗口业务逻辑
  - 实现INotifyPropertyChanged接口
  - 使用自定义RelayCommand处理命令
  - 支持异步操作和资源管理

#### 5. 视图层
- **MainWindow.xaml**: 主窗口UI定义
- **MainWindow.xaml.cs**: 窗口事件处理和系统API集成
- **App.xaml/App.xaml.cs**: 应用程序入口和启动逻辑

### MVVM架构实现

#### 数据绑定
- View层通过XAML数据绑定到ViewModel属性
- 使用双向绑定实现UI状态同步
- 命令绑定处理用户交互

#### 依赖注入
- 服务通过构造函数注入到ViewModel
- 接口分离原则，便于测试和扩展

## 关键功能特性

### 1. 剪贴板监听
- 实时监听剪贴板变化
- 支持文本、图片、文件等多种格式
- 防重复机制避免相同内容重复记录

### 2. 系统托盘集成
- 最小化到系统托盘
- 托盘右键菜单功能
- 托盘双击恢复窗口

### 3. 全局快捷键
- Alt+Z快捷键控制窗口显示/隐藏
- 窗口状态智能切换
- 使用Windows API注册热键

### 4. 搜索功能
- 实时搜索历史记录
- 支持内容模糊匹配

### 5. 开机启动
- 支持Windows开机启动
- 通过注册表实现
- 用户权限检查

### 6. 智能粘贴机制
- **双击粘贴**: 双击历史记录项自动复制到剪贴板并粘贴到之前活动窗口
- **窗口记忆**: 智能记录前一个活动窗口句柄，实现精准切换
- **延迟粘贴**: 使用Task.Delay确保窗口切换完成后再发送粘贴命令
- **键盘模拟**: 使用Windows API (SendInput) 实现精准的Ctrl+V粘贴操作
- **失败恢复**: 粘贴失败时自动恢复原窗口状态

## 开发注意事项

### 1. 窗口管理
- 应用程序支持单实例运行
- 窗口启动时在屏幕中央显示 (WindowStartupLocation.CenterScreen)
- 关闭窗口时最小化到托盘而非退出

### 2. 资源管理
- 实现IDisposable接口的服务需要正确释放资源
- 数据库连接和剪贴板监听需要适当清理

### 3. 异常处理
- 全局异常处理在App.xaml.cs中
- 用户操作需要友好的错误提示
- 调试信息通过Debug.WriteLine输出

### 4. 数据库操作
- 使用Entity Framework Core进行数据访问
- 所有数据库操作都应该是异步的
- 迁移文件在Migrations/目录中

### 5. 日志系统配置
- 应用程序使用Serilog进行结构化日志记录
- 配置文件：[logging.json](logging.json)
- 日志文件位置：程序目录/Logs/PasteList-{Date}.log
- 支持多级别日志：Trace、Debug、Info、Warning、Error、Critical
- 日志自动滚动，保留30天的历史记录
- 控制台输出用于开发调试

## 代码约定

### 1. 命名规范
- 私有字段使用下划线前缀: `_viewModel`
- 接口使用I前缀: `IClipboardService`
- 异步方法使用Async后缀: `LoadHistoryAsync`

### 2. 代码组织
- 每个类都有详细的XML文档注释
- 使用#region组织相关代码块
- 错误处理使用try-catch块

### 3. MVVM模式
- View层只包含UI相关代码
- ViewModel层处理业务逻辑
- Model层定义数据结构

## Windows API集成

### 核心Windows API调用
应用程序集成了多个Windows API以实现系统级功能：

#### 剪贴板监听
```csharp
[DllImport("user32.dll")]
private static extern bool AddClipboardFormatListener(IntPtr hwnd);

private const int WM_CLIPBOARDUPDATE = 0x031D;
```

#### 全局热键注册
```csharp
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll")]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```

#### 窗口管理
```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
private static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("user32.dll")]
private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
```

#### 键盘模拟
```csharp
[DllImport("user32.dll")]
private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
```

### API使用注意事项
- 所有Windows API调用都需要适当的错误处理
- 窗口句柄必须在窗口创建后才能使用
- 热键注册需要在窗口加载完成后进行
- 键盘模拟需要考虑UIPI (User Interface Privilege Isolation) 限制

## 异常处理策略

### 全局异常处理
应用程序在App.xaml.cs中实现了全局异常处理机制：

```csharp
// WPF线程异常处理
this.DispatcherUnhandledException += App_DispatcherUnhandledException;

// 非UI线程异常处理
AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
```

### 异常日志记录
- 所有异常都会记录到日志文件中
- 使用LoggerService记录详细的异常堆栈信息
- 错误日志包含时间戳、异常类型和完整堆栈
- 用户友好的错误提示，避免显示技术细节

### 资源清理和异常安全
- 所有实现IDisposable的服务都必须正确释放资源
- 使用try-finally确保资源清理
- 异常情况下的优雅降级处理
- 避免异常传播导致应用程序崩溃

## 调试和测试

### 1. 调试输出
- 使用Debug.WriteLine输出调试信息
- 状态消息通过ViewModel的StatusMessage属性显示

### 2. 数据库调试
- 数据库文件位于LocalApplicationData目录
- 可以使用SQLite浏览器工具查看数据

### 3. 系统集成测试
- 测试剪贴板功能时注意系统权限
- 全局快捷键需要窗口句柄才能注册

### 4. 日志分析和性能监控
- 查看日志文件：程序目录/Logs/PasteList-{Date}.log
- 监控剪贴板操作频率和性能指标
- 通过日志分析用户行为模式
- 检查异常日志识别潜在问题

### 5. 常见问题排查
- **剪贴板监听失败**: 检查应用程序权限和Windows剪贴板服务
- **热键不响应**: 确认窗口句柄有效，检查热键冲突
- **数据库错误**: 验证SQLite数据库文件权限，检查磁盘空间
- **托盘图标不显示**: 检查NotifyIcon初始化和系统托盘支持

### 6. 开发环境配置
- 使用Visual Studio 2022进行开发调试
- 推荐安装.NET 8.0 SDK和Entity Framework Core工具
- 配置SQLite数据库浏览器工具查看数据
- 启用详细日志输出进行问题诊断