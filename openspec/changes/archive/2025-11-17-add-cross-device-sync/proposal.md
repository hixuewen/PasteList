# 变更：跨设备剪贴板历史记录同步功能

## 为什么
当前版本的剪贴板历史记录仅存储在本地，用户在多台电脑上使用时无法共享历史记录，导致需要在不同设备间重复添加相同内容。通过添加跨设备同步功能，用户可以在多台电脑间无缝同步剪贴板历史，提升工作效率和用户体验。

## 变更内容
- 新增同步服务（ISyncService/SyncService）
- 新增同步配置管理（SyncConfiguration模型）
- 在设置窗口中添加同步方式配置选项
- 实现本地导出/导入功能作为基础同步机制
- 支持后续扩展云端同步（如OneDrive、百度网盘等）
- 添加同步状态指示和操作反馈
- 新增数据库表存储同步配置信息

## 影响
- **涉及的规格**：
  - 新增：sync 同步规格
- **涉及的核心代码**：
  - Models/SyncConfiguration.cs（新增）
  - Services/ISyncService/SyncService（新增）
  - Services/ISyncConfigurationService/SyncConfigurationService（新增）
  - ViewModels/SettingsViewModel（扩展）
  - Views/SettingsWindow.xaml（扩展）
  - Data/ClipboardDbContext.cs（扩展）
- **数据库变更**：新增SyncConfiguration表
- **用户界面**：设置窗口新增"同步方式"配置区域

## 架构决策要点
- 采用可插拔的同步策略模式，支持多种同步方式
- 优先实现本地导出/导入功能，为云端同步奠定基础
- 同步配置独立存储，不影响现有剪贴板数据结构
- 提供清晰的同步状态反馈和错误处理机制