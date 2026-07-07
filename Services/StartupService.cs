using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;

namespace PasteList.Services
{
    public interface IStartupService
    {
        bool IsStartupEnabled();
        void EnableStartup();
        void DisableStartup();
        void ToggleStartup();
    }

    public class StartupService : IStartupService
    {
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string REGISTRY_VALUE_NAME = "PasteList";
        private readonly ILoggerService? _logger;

        public StartupService(ILoggerService? logger = null)
        {
            _logger = logger;
        }

        public bool IsStartupEnabled()
        {
            try
            {
                string currentPath = GetApplicationPath();
                if (string.IsNullOrEmpty(currentPath))
                {
                    _logger?.LogWarning("无法获取应用程序路径，开机启动状态检测跳过");
                    return false;
                }

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false))
                {
                    if (key != null)
                    {
                        string? value = key.GetValue(REGISTRY_VALUE_NAME) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            bool enabled = value == currentPath;
                            _logger?.LogDebug($"开机启动注册表项: {value}, 当前路径: {currentPath}, 匹配结果: {enabled}");
                            return enabled;
                        }
                        _logger?.LogDebug("注册表中未找到 PasteList 启动项");
                    }
                    else
                    {
                        _logger?.LogWarning($"无法打开注册表键: {REGISTRY_KEY_PATH}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查开机启动状态时发生错误");
            }

            return false;
        }

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

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY_PATH))
                {
                    if (key != null)
                    {
                        key.SetValue(REGISTRY_VALUE_NAME, applicationPath, RegistryValueKind.String);
                        _logger?.LogUserAction("启用开机启动", $"路径: {applicationPath}");
                    }
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

        public void DisableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY_PATH))
                {
                    if (key != null)
                    {
                        key.DeleteValue(REGISTRY_VALUE_NAME, false);
                        _logger?.LogUserAction("禁用开机启动");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "禁用开机启动时发生错误");
                MessageBox.Show($"取消开机启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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