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

### Testing Strategy
- **开发阶段测试**:
  - 使用Debug.WriteLine输出调试信息
  - 通过日志系统监控应用行为
  - 手动测试剪贴板功能和系统集成
  - 数据库操作测试使用SQLite浏览器

- **质量保证**:
  - 编译时错误检查 (`dotnet build --no-restore`)
  - 代码审查和静态分析
  - 性能监控和日志分析
  - 用户体验测试和反馈收集

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
