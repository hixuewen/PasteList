using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace PasteList.Services
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        void EnableStartup();
        void DisableStartup();
        void ToggleStartup();
        void MigrateLegacyStartup();
    }

    /// <summary>
    /// 开机启动管理服务
    /// 通过任务计划程序注册"登录时启动"的计划任务，实现应用开机自动启动。
    /// 相比注册表 Run 键，计划任务支持启动延迟和失败重试，
    /// 可避免应用位于非系统盘时登录阶段磁盘尚未挂载导致的启动失败。
    /// </summary>
    public class StartupService : IStartupService
    {
        private const string TASK_NAME = "PasteList";
        private const string LEGACY_REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string LEGACY_REGISTRY_VALUE_NAME = "PasteList";
        private readonly ILoggerService? _logger;

        public StartupService(ILoggerService? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 检查是否已设置为开机启动
        /// 优先检查计划任务，其次兼容旧版注册表 Run 键配置
        /// </summary>
        public bool IsStartupEnabled()
        {
            try
            {
                if (IsTaskRegistered())
                {
                    _logger?.LogDebug("已注册开机启动计划任务");
                    return true;
                }

                string currentPath = GetApplicationPath();
                if (string.IsNullOrEmpty(currentPath))
                {
                    _logger?.LogWarning("无法获取应用程序路径，开机启动状态检测跳过");
                    return false;
                }

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(LEGACY_REGISTRY_KEY_PATH, false))
                {
                    if (key != null)
                    {
                        string? value = key.GetValue(LEGACY_REGISTRY_VALUE_NAME) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            bool enabled = value == currentPath;
                            _logger?.LogDebug($"注册表 Run 键启动项: {value}, 当前路径: {currentPath}, 匹配结果: {enabled}");
                            return enabled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查开机启动状态时发生错误");
            }

            return false;
        }

        /// <summary>
        /// 设置开机启动
        /// 优先注册登录触发的计划任务（带延迟和失败重试），
        /// 任务注册失败时回退到注册表 Run 键方式
        /// </summary>
        public void EnableStartup()
        {
            try
            {
                string applicationPath = GetApplicationPath();
                if (string.IsNullOrEmpty(applicationPath))
                {
                    _logger?.LogError("无法获取应用程序路径，开机启动启用失败");
                    MessageBox.Show("无法获取应用程序路径，开机启动设置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                bool taskRegistered = false;
                try
                {
                    taskRegistered = RegisterStartupTask(applicationPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"注册开机启动计划任务失败（{ex.Message}），回退到注册表 Run 键方式");
                }

                if (taskRegistered)
                {
                    RemoveLegacyRegistryValue();
                    _logger?.LogUserAction("启用开机启动", $"计划任务: {TASK_NAME}, 路径: {applicationPath}");
                }
                else
                {
                    WriteLegacyRegistryValue(applicationPath);
                    _logger?.LogUserAction("启用开机启动", $"注册表 Run 键, 路径: {applicationPath}");
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError("权限不足，无法设置开机启动");
                MessageBox.Show("权限不足，无法设置开机启动。请以管理员身份运行程序。", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启用开机启动时发生错误");
                MessageBox.Show($"设置开机启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消开机启动
        /// 删除计划任务并清理旧版注册表配置
        /// </summary>
        public void DisableStartup()
        {
            try
            {
                DeleteStartupTask();
                RemoveLegacyRegistryValue();
                _logger?.LogUserAction("禁用开机启动");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "禁用开机启动时发生错误");
                MessageBox.Show($"取消开机启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切换开机启动状态
        /// </summary>
        public void ToggleStartup()
        {
            if (IsStartupEnabled())
            {
                DisableStartup();
            }
            else
            {
                EnableStartup();
            }
        }

        /// <summary>
        /// 迁移旧版注册表 Run 键配置到计划任务
        /// 兼容旧版本（仅注册表方式）配置的用户，避免设置窗口勾选状态
        /// 与真实生效方式不一致，也避免遗留 Run 键导致登录时重复启动
        /// </summary>
        public void MigrateLegacyStartup()
        {
            try
            {
                if (IsTaskRegistered())
                {
                    RemoveLegacyRegistryValue();
                    return;
                }

                string currentPath = GetApplicationPath();
                if (string.IsNullOrEmpty(currentPath))
                {
                    return;
                }

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(LEGACY_REGISTRY_KEY_PATH, false))
                {
                    string? value = key?.GetValue(LEGACY_REGISTRY_VALUE_NAME) as string;
                    if (!string.IsNullOrEmpty(value) && value == currentPath)
                    {
                        _logger?.LogInfo("检测到旧版注册表开机启动配置，正在迁移到计划任务");
                        if (RegisterStartupTask(currentPath))
                        {
                            RemoveLegacyRegistryValue();
                            _logger?.LogUserAction("迁移开机启动配置到计划任务", $"计划任务: {TASK_NAME}, 路径: {currentPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "迁移开机启动配置时发生错误");
            }
        }

        /// <summary>
        /// 注册登录触发的计划任务
        /// 延迟 10 秒启动，失败后每 1 分钟重试一次（最多 3 次），
        /// 确保非系统盘在登录时已挂载完成
        /// </summary>
        /// <param name="applicationPath">应用程序完整路径</param>
        /// <returns>注册是否成功</returns>
        private bool RegisterStartupTask(string applicationPath)
        {
            using (TaskService taskService = new TaskService())
            {
                TaskDefinition taskDefinition = taskService.NewTask();
                taskDefinition.RegistrationInfo.Description = "PasteList 剪贴板历史记录 - 开机自动启动";
                taskDefinition.RegistrationInfo.Author = Environment.UserName;

                // 登录时触发，仅当前用户，延迟 10 秒等待磁盘挂载完成
                LogonTrigger trigger = new LogonTrigger
                {
                    UserId = Environment.UserName,
                    Delay = TimeSpan.FromSeconds(10)
                };
                taskDefinition.Triggers.Add(trigger);

                // 启动应用（ExecAction 需要不带引号的真实路径），工作目录设置为程序所在目录
                string unquotedPath = applicationPath.Trim('"');
                string? workingDirectory = Path.GetDirectoryName(unquotedPath);
                taskDefinition.Actions.Add(new ExecAction(unquotedPath, null, workingDirectory));

                // 启动失败时重试（覆盖磁盘尚未挂载的情况）
                // 注意：Task Scheduler 要求重试间隔最短 1 分钟
                taskDefinition.Settings.RestartCount = 3;
                taskDefinition.Settings.RestartInterval = TimeSpan.FromMinutes(1);

                // 允许电池供电时启动，不因电源变化停止
                taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                taskDefinition.Settings.StopIfGoingOnBatteries = false;

                // 应用常驻托盘，不限制执行时间
                taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                taskService.RootFolder.RegisterTaskDefinition(
                    TASK_NAME,
                    taskDefinition,
                    TaskCreation.CreateOrUpdate,
                    null,
                    null,
                    TaskLogonType.InteractiveToken,
                    null);
            }

            return true;
        }

        /// <summary>
        /// 删除开机启动计划任务
        /// </summary>
        private void DeleteStartupTask()
        {
            using (TaskService taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask(TASK_NAME, true);
            }
        }

        /// <summary>
        /// 检查开机启动计划任务是否已注册
        /// </summary>
        private bool IsTaskRegistered()
        {
            using (TaskService taskService = new TaskService())
            {
                return taskService.RootFolder
                    .EnumerateTasks(task => string.Equals(task.Name, TASK_NAME, StringComparison.OrdinalIgnoreCase), false)
                    .Any();
            }
        }

        /// <summary>
        /// 删除旧版注册表 Run 键配置
        /// 用于从旧实现迁移，避免登录时重复启动
        /// </summary>
        private void RemoveLegacyRegistryValue()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(LEGACY_REGISTRY_KEY_PATH, true))
                {
                    if (key != null && key.GetValue(LEGACY_REGISTRY_VALUE_NAME) != null)
                    {
                        key.DeleteValue(LEGACY_REGISTRY_VALUE_NAME, false);
                        _logger?.LogDebug("已清理旧版注册表 Run 键启动项");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理旧版注册表启动项时发生错误");
            }
        }

        /// <summary>
        /// 写入旧版注册表 Run 键配置（计划任务注册失败时的回退方案）
        /// </summary>
        /// <param name="applicationPath">应用程序完整路径</param>
        private void WriteLegacyRegistryValue(string applicationPath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(LEGACY_REGISTRY_KEY_PATH))
            {
                if (key != null)
                {
                    key.SetValue(LEGACY_REGISTRY_VALUE_NAME, applicationPath, RegistryValueKind.String);
                    _logger?.LogDebug("已写入注册表 Run 键启动项");
                }
            }
        }

        /// <summary>
        /// 获取应用程序完整路径（带引号，兼容注册表 Run 键格式）
        /// </summary>
        private string GetApplicationPath()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    _logger?.LogWarning("Process.MainModule?.FileName 返回空");
                    return string.Empty;
                }

                if (!exePath.StartsWith("\""))
                {
                    exePath = $"\"{exePath}\"";
                }

                return exePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取应用程序路径时发生错误");
                return string.Empty;
            }
        }
    }
}
