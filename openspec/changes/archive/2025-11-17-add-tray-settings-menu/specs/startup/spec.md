# 开机启动功能变更规格

## MODIFIED Requirements
### Requirement: 开机启动设置入口
开机启动设置 SHALL 通过设置窗口访问，而非直接通过托盘菜单访问。

**变更前**: 通过托盘菜单中的"开机启动"复选框直接切换状态
**变更后**: 通过托盘菜单 → 设置窗口中的开机启动选项切换状态

#### Scenario: 在设置窗口中启用开机启动
- **WHEN** 用户在设置窗口中勾选开机启动选项并点击确定
- **THEN** 系统 SHALL 启用开机启动，并显示成功提示
- **AND** 窗口 SHALL 关闭并返回托盘

#### Scenario: 在设置窗口中禁用开机启动
- **WHEN** 用户在设置窗口中取消勾选开机启动选项并点击确定
- **THEN** 系统 SHALL 禁用开机启动，并显示成功提示
- **AND** 窗口 SHALL 关闭并返回托盘

#### Scenario: 取消设置窗口
- **WHEN** 用户在设置窗口中修改了设置但点击取消按钮
- **THEN** 所有修改 SHALL 被丢弃
- **AND** 窗口 SHALL 关闭且不保存任何更改

#### Scenario: 设置窗口加载时显示当前状态
- **WHEN** 设置窗口打开
- **THEN** 开机启动复选框 SHALL 显示当前的启用/禁用状态
- **AND** 状态 SHALL 与StartupService.IsStartupEnabled()的返回值一致
