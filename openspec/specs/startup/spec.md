# 开机启动规范

## Purpose
本规范定义剪贴板历史记录应用程序的开机启动管理功能，确保用户可以选择在系统启动时自动运行应用程序。

## Requirements

### Requirement: 开机启动设置入口
开机启动设置 SHALL 通过设置窗口访问。

#### Scenario: 在设置窗口中启用开机启动
- **WHEN** 用户在设置窗口中勾选开机启动选项并点击确定
- **THEN** 系统 SHALL 启用开机启动
- **AND** 窗口 SHALL 关闭并返回托盘

#### Scenario: 在设置窗口中禁用开机启动
- **WHEN** 用户在设置窗口中取消勾选开机启动选项并点击确定
- **THEN** 系统 SHALL 禁用开机启动
- **AND** 窗口 SHALL 关闭并返回托盘

#### Scenario: 取消设置窗口
- **WHEN** 用户在设置窗口中修改了设置但点击取消按钮
- **THEN** 所有修改 SHALL 被丢弃
- **AND** 窗口 SHALL 关闭且不保存任何更改

#### Scenario: 设置窗口加载时显示当前状态
- **WHEN** 设置窗口打开
- **THEN** 开机启动复选框 SHALL 显示当前的启用/禁用状态
- **AND** 状态 SHALL 与StartupService.IsStartupEnabled()的返回值一致

### Requirement: 开机启动状态检查
系统 SHALL 提供接口检查当前开机启动状态。

#### Scenario: 检查开机启动状态
- **WHEN** 调用StartupService.IsStartupEnabled()方法
- **THEN** 方法 SHALL 返回布尔值表示是否已启用开机启动
- **AND** 返回值 SHALL 反映注册表中的实际状态

### Requirement: 启用开机启动
系统 SHALL 支持通过注册表启用开机启动。

#### Scenario: 启用开机启动
- **WHEN** 调用StartupService.EnableStartup()方法
- **THEN** 系统 SHALL 在注册表中创建启动项
- **AND** 下次系统启动时应用程序 SHALL 自动运行

### Requirement: 禁用开机启动
系统 SHALL 支持通过注册表禁用开机启动。

#### Scenario: 禁用开机启动
- **WHEN** 调用StartupService.DisableStartup()方法
- **THEN** 系统 SHALL 从注册表中删除启动项
- **AND** 下次系统启动时应用程序 SHALL 不会自动运行

### Requirement: 切换开机启动状态
系统 SHALL 支持切换开机启动的启用/禁用状态。

#### Scenario: 切换开机启动状态
- **WHEN** 调用StartupService.ToggleStartup()方法
- **THEN** 如果当前已启用则禁用，反之则启用
- **AND** 返回新的状态
