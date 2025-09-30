# PasteList - 智能剪贴板历史记录管理器

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows%20Desktop-5C2D91.svg)](https://github.com/dotnet/wpf)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

一款功能强大、设计精美的剪贴板历史记录管理工具，基于 .NET 8.0 和 WPF 开发，采用 MVVM 架构模式，为您提供高效的剪贴板管理体验。

## ✨ 核心特性

### 🎯 智能剪贴板监听
- **实时监听**: 自动捕获剪贴板内容变化
- **防重复机制**: 智能过滤重复内容，避免冗余记录
- **多格式支持**: 支持文本内容（可扩展至图片、文件等格式）

### 🖥️ 现代化用户界面
- **响应式设计**: 适配不同屏幕尺寸，支持最小/最大尺寸限制
- **直观操作**: 清晰的界面布局，操作简单明了
- **实时搜索**: 即时搜索历史记录，快速定位需要的内容
- **状态提示**: 实时显示应用状态和操作反馈

### ⚡ 智能粘贴功能
- **双击粘贴**: 双击历史记录项自动粘贴到之前活动窗口
- **窗口切换**: 智能记忆并切换到之前的活动应用程序
- **键盘模拟**: 使用 Windows API 实现精准的 Ctrl+V 粘贴操作

### 🔧 系统集成
- **全局快捷键**: Alt+Z 快速显示/隐藏主窗口
- **系统托盘**: 最小化到托盘，不占用任务栏空间
- **开机启动**: 支持开机自动启动，随时待命
- **右键菜单**: 便捷的托盘右键菜单操作

### 🗄️ 数据管理
- **SQLite 数据库**: 轻量级本地数据库，性能优异
- **分页加载**: 高效的数据加载机制，支持大量历史记录
- **CRUD 操作**: 完整的增删查改功能
- **数据安全**: 本地存储，保护隐私安全

## 🏗️ 技术架构

### 核心技术栈
- **.NET 8.0**: 最新的 .NET 框架，性能优异
- **WPF**: 原生 Windows 桌面应用框架
- **Entity Framework Core 9.0.8**: 现代化的 ORM 框架
- **SQLite**: 轻量级嵌入式数据库
- **Hardcodet.NotifyIcon.Wpf**: 专业的系统托盘组件

### MVVM 架构设计
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│      View       │    │   ViewModel     │    │     Model       │
│                 │    │                 │    │                 │
│ MainWindow.xaml │◄──►│MainWindowVM.cs  │◄──►│ ClipboardItem   │
│ - UI 布局       │    │ - 业务逻辑      │    │ - 数据实体      │
│ - 数据绑定      │    │ - 命令处理      │    │ - 数据验证      │
│ - 用户交互      │    │ - 状态管理      │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                    ┌─────────────────┐
                    │   Service Layer │
                    │                 │
                    │ ClipboardService│
                    │HistoryService   │
                    │StartupService   │
                    └─────────────────┘
```

### 项目结构
```
PasteList/
├── Models/                 # 数据模型层
│   └── ClipboardItem.cs    # 剪贴板项目实体
├── Data/                   # 数据访问层
│   └── ClipboardDbContext.cs # EF 数据库上下文
├── Services/               # 业务服务层
│   ├── IClipboardService.cs    # 剪贴板服务接口
│   ├── ClipboardService.cs     # 剪贴板服务实现
│   ├── IClipboardHistoryService.cs # 历史记录服务接口
│   ├── ClipboardHistoryService.cs  # 历史记录服务实现
│   └── StartupService.cs     # 开机启动服务
├── ViewModels/             # 视图模型层
│   └── MainWindowViewModel.cs # 主窗口视图模型
├── View/                   # 视图层
│   ├── MainWindow.xaml     # 主窗口界面
│   └── MainWindow.xaml.cs  # 主窗口代码后置
├── Migrations/             # 数据库迁移文件
├── Resources/              # 资源文件
└── CLAUDE.md              # 项目开发文档
```

## 🚀 快速开始

### 系统要求
- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET 8.0 Runtime 或更高版本
- **内存**: 最少 100MB 可用内存
- **磁盘**: 最少 50MB 可用空间

### 安装方式

#### 方式一：下载发布版本（推荐）
1. 前往 [Releases](https://github.com/yourusername/PasteList/releases) 页面
2. 下载最新版本的 `PasteList.zip`
3. 解压到任意文件夹
4. 运行 `PasteList.exe`

#### 方式二：从源码构建
```bash
# 克隆仓库
git clone https://github.com/yourusername/PasteList.git
cd PasteList

# 恢复依赖
dotnet restore

# 构建项目
dotnet build --configuration Release

# 运行应用程序
dotnet run --configuration Release
```

### 数据库迁移
如需手动迁移数据库：
```bash
# 创建新迁移
dotnet ef migrations add MigrationName --project PasteList.csproj

# 更新数据库
dotnet ef database update --project PasteList.csproj
```

## 📖 使用指南

### 基本操作
1. **启动监听**: 点击"开始监听"按钮开始捕获剪贴板内容
2. **查看历史**: 在列表中查看所有剪贴板历史记录
3. **搜索内容**: 在搜索框中输入关键词快速定位
4. **使用内容**: 双击任意项目自动粘贴到之前的应用程序
5. **删除记录**: 右键点击项目选择删除

### 快捷键
- **Alt+Z**: 显示/隐藏主窗口
- **双击项目**: 快速粘贴到之前活动窗口

### 系统托盘
- **左键点击**: 显示/隐藏主窗口
- **右键点击**: 显示菜单（显示窗口、开机启动、退出）

## 🛠️ 开发指南

### 构建环境
- **IDE**: Visual Studio 2022 或 Visual Studio Code
- **SDK**: .NET 8.0 SDK
- **数据库**: SQLite (无需额外安装)

### 调试技巧
- 使用 `Debug.WriteLine()` 输出调试信息
- 数据库文件位置: `%LocalAppData%/PasteList/clipboard.db`
- 可以使用 SQLite 浏览器工具查看数据

### 代码规范
- 私有字段使用下划线前缀: `_viewModel`
- 接口使用 I 前缀: `IClipboardService`
- 异步方法使用 Async 后缀: `LoadHistoryAsync`
- 所有公共 API 都包含详细的 XML 文档注释

## 🤝 贡献指南

欢迎贡献代码！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支: `git checkout -b feature/AmazingFeature`
3. 提交更改: `git commit -m 'Add some AmazingFeature'`
4. 推送分支: `git push origin feature/AmazingFeature`
5. 提交 Pull Request

## 📝 更新日志

### v1.0.0 (2024-09-01)
- ✨ 初始版本发布
- 🎯 基础剪贴板监听功能
- 🖥️ 现代化 WPF 界面
- ⚡ 智能粘贴功能
- 🔧 系统托盘集成
- 🔍 搜索功能
- 🗄️ SQLite 数据存储

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Microsoft](https://www.microsoft.com/) - 提供 .NET 和 WPF 框架
- [Hardcodet](https://github.com/hardcodet) - 优秀的 NotifyIcon.Wpf 组件
- 所有为这个项目提供反馈和建议的用户

---

## 💬 联系作者

如果您对项目有任何问题、建议或合作意向，欢迎通过以下方式联系我：

### 📱 微信交流
![微信二维码](assets/wechat-qr-code.png)

**扫码添加微信，请备注"GitHub-PasteList"，方便我及时通过好友申请！**

无论是：
- 🐛 **Bug 反馈**: 遇到任何问题或异常
- 💡 **功能建议**: 有好的想法或改进建议
- 🤝 **技术交流**: .NET/WPF 开发经验分享
- 📈 **商业合作**: 企业定制或技术合作

都非常欢迎添加微信进行深入交流！

---

**⭐ 如果这个项目对您有帮助，请给个 Star 支持一下！**