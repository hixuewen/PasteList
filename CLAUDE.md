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
  - 使用Windows API监听剪贴板变化
  - 支持防重复机制

- **IClipboardHistoryService/ClipboardHistoryService**: 历史记录管理服务
  - 提供CRUD操作
  - 支持搜索和分页

- **StartupService**: 开机启动管理服务
  - 通过Windows注册表实现

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