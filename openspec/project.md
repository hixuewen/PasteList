# Project Context

## Purpose
PasteList是一个基于.NET 8.0和WPF的剪贴板历史记录管理应用程序。该项目旨在提供用户友好的剪贴板内容监听、存储和管理解决方案，支持实时监听剪贴板变化、历史记录管理、系统托盘集成、全局快捷键操作和开机自启动等功能。

**核心目标：**
- 提供稳定可靠的剪贴板监听服务
- 实现高效的历史记录管理和检索
- 创建直观易用的用户界面
- 支持多种剪贴板内容格式（文本、图片、文件路径等）
- 确保数据安全和隐私保护

## Tech Stack
- **框架**: .NET 8.0 (Windows Desktop)
- **UI框架**: WPF (Windows Presentation Foundation)
- **数据库**: SQLite + Entity Framework Core 9.0.8
- **架构模式**: MVVM (Model-View-ViewModel)
- **系统托盘**: Hardcodet.NotifyIcon.Wpf 1.1.0
- **日志框架**: Serilog (结构化日志记录)
- **开发工具**: Visual Studio 2022

### 项目结构
```
PasteList/
├── Data/                          # 数据访问层
│   └── ClipboardDbContext.cs     # EF数据库上下文
├── Models/                        # 数据模型
│   └── ClipboardItem.cs          # 剪贴板项目实体
├── Services/                      # 服务层
│   ├── ClipboardService.cs       # 剪贴板监听服务
│   ├── ClipboardHistoryService.cs # 历史记录管理
│   ├── StartupService.cs         # 开机启动服务
│   └── LoggerService.cs          # 日志服务
├── ViewModels/                    # 视图模型层
│   └── MainWindowViewModel.cs    # 主窗口业务逻辑
├── Views/                         # 视图层
│   ├── MainWindow.xaml           # 主窗口UI定义
│   └── MainWindow.xaml.cs        # 窗口事件处理
├── Migrations/                    # 数据库迁移文件
├── logging.json                   # 日志配置文件
└── PasteList.csproj              # 项目文件
```

## Project Conventions

### Code Style
- **命名规范**:
  - 私有字段使用下划线前缀: `_viewModel`, `_clipboardService`
  - 接口使用I前缀: `IClipboardService`, `ILoggerService`
  - 异步方法使用Async后缀: `LoadHistoryAsync`, `AddItemAsync`
  - 类和方法使用PascalCase命名
  - 变量和参数使用camelCase命名

- **代码格式**:
  - 启用可空引用类型 (`<Nullable>enable</Nullable>`)
  - 启用隐式using语句 (`<ImplicitUsings>enable</ImplicitUsings>`)
  - 详细的XML文档注释用于所有公共成员
  - 使用`#region`组织相关代码块
  - 遵循C# 12.0最佳实践

- **错误处理**:
  - 全局异常处理在App.xaml.cs中实现
  - 用户友好的错误提示，避免技术细节
  - 使用try-catch块处理异常情况
  - 实现IDisposable接口确保资源正确释放

### Architecture Patterns
- **MVVM模式**:
  - View层: XAML文件，只包含UI相关代码
  - ViewModel层: 业务逻辑和数据绑定，实现INotifyPropertyChanged
  - Model层: 数据模型和实体类
  - 依赖注入: 通过构造函数注入服务

- **服务层架构**:
  - 接口分离: 每个服务都有对应的接口定义
  - 单一职责: 每个服务专注于特定功能领域
  - 异步操作: 所有数据库和IO操作都是异步的
  - 生命周期管理: 实现IDisposable确保资源清理

- **数据访问模式**:
  - Entity Framework Core用于数据持久化
  - Repository模式通过服务层实现
  - 使用SQLite作为嵌入式数据库
  - 数据库迁移管理架构变更

### Development Commands
- **构建和运行**:
  ```bash
  # 恢复依赖
  dotnet restore

  # 构建项目
  dotnet build

  # 运行应用程序
  dotnet run

  # 发布 Release 版本
  dotnet publish -c Release -r win-x64 --self-contained

  # 编译检查错误
  dotnet build --no-restore
  ```

- **数据库迁移**:
  ```bash
  # 创建新的迁移
  dotnet ef migrations add [MigrationName] --project PasteList.csproj

  # 更新数据库
  dotnet ef database update --project PasteList.csproj
  ```

- **测试**:
  ```bash
  # 运行所有测试
  dotnet test
  ```

### Testing Strategy
- **开发阶段测试**:
  - 使用Debug.WriteLine输出调试信息
  - 通过日志系统监控应用行为
  - 手动测试剪贴板功能和系统集成
  - 数据库操作测试使用SQLite浏览器
  - 查看日志文件：`程序目录/Logs/PasteList-{Date}.log`

- **质量保证**:
  - 编译时错误检查 (`dotnet build --no-restore`)
  - 代码审查和静态分析
  - 性能监控和日志分析
  - 用户体验测试和反馈收集

### Logging Configuration
- **日志框架**: Serilog
- **配置文件**: `logging.json`
- **日志位置**: 程序目录/Logs/PasteList-{Date}.log
- **日志级别**: Trace, Debug, Info, Warning, Error, Critical
- **日志特性**:
  - 自动滚动，保留30天历史记录
  - 控制台输出用于开发调试
  - 剪贴板操作专项日志记录
  - 性能监控和用户操作审计

### Git Workflow
- **分支策略**:
  - `main`: 生产就绪代码分支
  - `develop`: 开发主分支，当前工作分支
  - 功能分支: 从develop创建，完成后合并回develop
  - 修复分支: 用于紧急修复和bug解决

- **提交消息规范**:
  - 使用中文提交消息
  - 格式: `类型(范围): 描述`
  - 类型包括: `feat`(新功能), `fix`(修复), `refactor`(重构), `docs`(文档), `chore`(杂务)
  - 示例: "feat(界面): 添加窗口启动时在屏幕中央显示功能"
  - 示例: "fix: 修复剪贴板监听启动异常并移除自动启动逻辑"

- **代码审查**:
  - 所有代码变更需要自检
  - 确保编译无错误
  - 更新相关文档
  - 测试核心功能

## Domain Context
- **剪贴板监听机制**:
  - 使用Windows API `AddClipboardFormatListener` 实现实时监听
  - 处理 `WM_CLIPBOARDUPDATE` 消息来响应剪贴板变化
  - 支持多种格式：文本、图片(Base64编码)、文件路径列表
  - 智能防重复机制，避免相同内容重复记录

- **数据存储模型**:
  - `ClipboardItem` 实体包含剪贴板内容和元数据
  - 数据库文件位于 `%LocalAppData%/PasteList/clipboard.db`
  - 支持文本、图片和文件路径的统一存储
  - 异步数据库操作确保UI响应性

- **系统集成特性**:
  - 系统托盘集成支持最小化运行
  - 全局快捷键 Alt+Z 控制窗口显示/隐藏
  - 开机启动通过Windows注册表实现
  - 双击历史记录项实现智能粘贴功能

- **智能粘贴机制**:
  - 记录前一个活动窗口句柄
  - 精准窗口切换和延迟粘贴
  - 使用 Windows API `SendInput` 模拟Ctrl+V操作
  - 失败恢复机制确保用户体验

## Important Constraints
- **技术约束**:
  - 仅支持Windows平台（Windows API依赖）
  - 需要.NET 8.0 Desktop运行时
  - SQLite数据库文件访问权限要求
  - 系统剪贴板API使用限制

- **性能约束**:
  - 剪贴板监听不能阻塞UI线程
  - 数据库操作必须异步执行
  - 历史记录查询需要分页支持
  - 内存使用需要控制在合理范围内

- **安全约束**:
  - 敏感数据不应存储到数据库
  - 用户隐私保护要求
  - 系统权限最小化原则
  - 防止剪贴板数据泄露

- **用户体验约束**:
  - 单实例运行模式
  - 友好的错误提示消息
  - 快速响应的用户界面
  - 简洁直观的操作流程

## External Dependencies
- **核心依赖包**:
  - `Microsoft.EntityFrameworkCore.Sqlite 9.0.8`: 数据持久化层
  - `Microsoft.EntityFrameworkCore.Tools 9.0.8`: 数据库迁移工具
  - `Hardcodet.NotifyIcon.Wpf 1.1.0`: 系统托盘功能支持

- **系统API依赖**:
  - Windows剪贴板API (`user32.dll`)
  - 全局热键注册API (`RegisterHotKey`, `UnregisterHotKey`)
  - 窗口管理API (`GetForegroundWindow`, `SetForegroundWindow`)
  - 键盘模拟API (`SendInput`, `keybd_event`)

- **日志系统依赖**:
  - Serilog日志框架
  - 文件滚动输出配置
  - 多级别日志记录支持
  - 控制台和文件双重输出

- **开发工具依赖**:
  - Visual Studio 2022 IDE
  - .NET 8.0 SDK
  - Entity Framework Core CLI工具
  - SQLite数据库浏览器工具

### Common Troubleshooting

#### 1. 剪贴板监听失败
- **症状**: 剪贴板变化未被检测到
- **排查**:
  - 检查应用程序权限（需要访问剪贴板）
  - 验证Windows剪贴板服务是否运行
  - 查看日志文件中的异常信息
- **解决方案**: 重启应用程序或检查Windows权限设置

#### 2. 全局快捷键不响应
- **症状**: Alt+Z快捷键无效果
- **排查**:
  - 确认窗口句柄有效（必须在窗口加载后注册热键）
  - 检查热键是否已被其他程序占用
  - 查看MainWindow.xaml.cs中的RegisterHotKey调用
- **解决方案**: 重启应用程序或关闭冲突程序

#### 3. 数据库错误
- **症状**: 数据保存失败或查询异常
- **排查**:
  - 验证SQLite数据库文件权限（位于%LocalAppData%/PasteList/clipboard.db）
  - 检查磁盘空间是否充足
  - 查看日志中的Entity Framework错误信息
- **解决方案**: 检查文件权限，清理磁盘空间，重建数据库迁移

#### 4. 系统托盘图标不显示
- **症状**: 最小化后托盘无图标
- **排查**:
  - 检查NotifyIcon初始化代码
  - 验证Hardcodet.NotifyIcon.Wpf包是否正确安装
  - 确认操作系统托盘功能正常
- **解决方案**: 重新安装应用程序或重启资源管理器

#### 5. 智能粘贴失败
- **症状**: 双击历史记录无法粘贴到目标窗口
- **排查**:
  - 检查UIPI（User Interface Privilege Isolation）限制
  - 验证SendInput API调用权限
  - 确认目标窗口是否可接收键盘输入
- **解决方案**: 以管理员权限运行或检查目标应用程序安全策略

### Windows API Integration Details

#### 核心API调用
```csharp
// 剪贴板监听
[DllImport("user32.dll")]
private static extern bool AddClipboardFormatListener(IntPtr hwnd);
private const int WM_CLIPBOARDUPDATE = 0x031D;

// 全局热键注册
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
[DllImport("user32.dll")]
private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

// 窗口管理
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")]
private static extern bool SetForegroundWindow(IntPtr hWnd);

// 键盘模拟
[DllImport("user32.dll")]
private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
```

#### API使用注意事项
- 所有Windows API调用都需要适当的错误处理
- 窗口句柄必须在窗口创建后才能使用
- 热键注册需要在窗口加载完成后进行
- 键盘模拟需要考虑UIPI限制，确保应用程序有足够权限
- 实现IDisposable接口的服务必须正确释放API资源
- 使用try-finally确保在异常情况下也能正确清理资源

### Exception Handling Strategy

#### 全局异常处理
应用程序在 `App.xaml.cs` 中实现了完整的全局异常处理机制：

```csharp
// WPF线程异常处理
this.DispatcherUnhandledException += App_DispatcherUnhandledException;

// 非UI线程异常处理
AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
```

#### 异常日志记录
- 所有异常都会记录到日志文件中
- 使用 `LoggerService` 记录详细的异常堆栈信息
- 错误日志包含：时间戳、异常类型、完整堆栈跟踪
- 用户友好的错误提示，避免显示技术细节
- 关键异常会被标记为 `Error` 或 `Critical` 级别

#### 资源清理和异常安全
- 所有实现 `IDisposable` 的服务都必须正确释放资源
- 使用 `try-finally` 确保资源清理
- 异常情况下的优雅降级处理
- 避免异常传播导致应用程序崩溃
- 关键服务（剪贴板监听、数据库连接）有独立的异常恢复机制

#### 异常分类处理
1. **剪贴板相关异常**: 记录并继续监听，不中断服务
2. **数据库异常**: 记录错误，尝试重新连接或回滚事务
3. **UI异常**: 捕获并显示友好提示，不影响核心功能
4. **系统API异常**: 记录详细信息，提供故障排除建议
5. **启动异常**: 记录到日志，优雅退出或降级模式启动

### Key File Locations

#### 应用程序文件
- **可执行文件**: `bin\Debug\net8.0-windows\PasteList.exe` 或发布后的安装目录
- **数据库文件**: `%LocalAppData%\PasteList\clipboard.db`
- **配置文件**: `%LocalAppData%\PasteList\logging.json`（如果存在）
- **日志目录**: `程序目录\Logs\PasteList-{Date}.log`

#### 项目核心文件
- **应用程序入口**: `App.xaml` / `App.xaml.cs`
- **主窗口**: `Views\MainWindow.xaml` / `Views\MainWindow.xaml.cs:12`
- **主窗口ViewModel**: `ViewModels\MainWindowViewModel.cs:15`
- **剪贴板服务**: `Services\ClipboardService.cs:18`
- **历史记录服务**: `Services\ClipboardHistoryService.cs:21`
- **数据库上下文**: `Data\ClipboardDbContext.cs:10`
- **剪贴板模型**: `Models\ClipboardItem.cs:8`
- **日志配置**: `logging.json`
- **项目文件**: `PasteList.csproj`

#### 数据库迁移
- **迁移文件**: `Migrations\{Timestamp}_{MigrationName}.cs`
- **快照文件**: `Migrations\ClipboardDbContextModelSnapshot.cs`

#### 开发环境配置
- **解决方案文件**: `PasteList.sln`（如果存在）
- **用户设置**: `.vs\PasteList\v17\*.suo`（本地Visual Studio设置）
- **包还原缓存**: `%userprofile%\.nuget\packages\`

### Data Storage Details

#### 剪贴板项目结构
```csharp
public class ClipboardItem
{
    public int Id { get; set; }                    // 主键
    public string Content { get; set; }            // 内容（文本或Base64）
    public string ContentType { get; set; }        // 内容类型：Text/Image/FilePath
    public DateTime CreatedAt { get; set; }        // 创建时间
    public string? Preview { get; set; }           // 预览文本（可选）
}
```

#### 数据库表结构
- **表名**: `ClipboardItems`
- **索引**: `CreatedAt`（降序）、`ContentType`
- **约束**: `Id` 主键自增，`Content` 非空
- **编码**: UTF-8，支持中文和特殊字符

#### 数据保留策略
- 应用程序不主动删除历史记录
- 用户可通过界面手动删除条目
- 建议定期清理或归档（未来功能）
- 数据库文件大小监控和优化（待实现）
