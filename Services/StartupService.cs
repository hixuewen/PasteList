using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PasteList.Services
{
    /// <summary>
    /// 开机启动管理服务
    /// 通过操作注册表实现应用程序的开机启动设置
    /// </summary>
    public interface IStartupService
    {
        /// <summary>
        /// 检查是否已设置为开机启动
        /// </summary>
        bool IsStartupEnabled();
        
        /// <summary>
        /// 设置开机启动
        /// </summary>
        void EnableStartup();
        
        /// <summary>
        /// 取消开机启动
        /// </summary>
        void DisableStartup();
        
        /// <summary>
        /// 切换开机启动状态
        /// </summary>
        void ToggleStartup();
    }
    
    /// <summary>
    /// 开机启动服务实现
    /// </summary>
    public class StartupService : IStartupService
    {
        private const string REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string REGISTRY_VALUE_NAME = "PasteList";
        
        /// <summary>
        /// 检查是否已设置为开机启动
        /// </summary>
        public bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false))
                {
                    if (key != null)
                    {
                        string? value = key.GetValue(REGISTRY_VALUE_NAME) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            // 验证路径是否指向当前应用程序
                            string currentPath = GetApplicationPath();
                            return value.Contains(currentPath) || value == currentPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查开机启动状态时发生错误: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// 设置开机启动
        /// </summary>
        public void EnableStartup()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true))
                {
                    if (key != null)
                    {
                        string applicationPath = GetApplicationPath();
                        key.SetValue(REGISTRY_VALUE_NAME, applicationPath, RegistryValueKind.String);
                        Debug.WriteLine("开机启动已启用");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("权限不足，无法设置开机启动。请以管理员身份运行程序。", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启用开机启动时发生错误: {ex.Message}");
                MessageBox.Show($"设置开机启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 取消开机启动
        /// </summary>
        public void DisableStartup()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(REGISTRY_VALUE_NAME, false);
                        Debug.WriteLine("开机启动已禁用");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"禁用开机启动时发生错误: {ex.Message}");
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
        /// 获取应用程序完整路径
        /// </summary>
        private string GetApplicationPath()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                
                // 确保路径存在引号，防止路径中包含空格时出错
                if (!string.IsNullOrEmpty(exePath) && !exePath.StartsWith("\""))
                {
                    exePath = $"\"{exePath}\"";
                }
                
                return exePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取应用程序路径时发生错误: {ex.Message}");
                return string.Empty;
            }
        }
    }
}