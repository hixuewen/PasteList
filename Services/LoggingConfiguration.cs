using System;
using System.IO;
using System.Text.Json;

namespace PasteList.Services
{
    /// <summary>
    /// 日志配置类
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// 是否启用控制台输出
        /// </summary>
        public bool EnableConsoleOutput { get; set; } = true;

        /// <summary>
        /// 是否启用文件输出
        /// </summary>
        public bool EnableFileOutput { get; set; } = true;

        /// <summary>
        /// 日志文件保留天数
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// 日志文件最大大小（MB）
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 10;

        /// <summary>
        /// 是否启用性能日志
        /// </summary>
        public bool EnablePerformanceLogging { get; set; } = true;

        /// <summary>
        /// 是否启用剪贴板操作日志
        /// </summary>
        public bool EnableClipboardLogging { get; set; } = true;

        /// <summary>
        /// 是否启用用户操作日志
        /// </summary>
        public bool EnableUserActionLogging { get; set; } = true;

        /// <summary>
        /// 日志输出模板
        /// </summary>
        public string OutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Category}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// 从配置文件加载配置
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <returns>日志配置实例</returns>
        public static LoggingConfiguration LoadFromFile(string configPath = "logging.json")
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<LoggingConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return config ?? new LoggingConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载日志配置文件失败: {ex.Message}");
            }

            return new LoggingConfiguration();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        public void SaveToFile(string configPath = "logging.json")
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存日志配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建开发环境配置
        /// </summary>
        public static LoggingConfiguration CreateDevelopmentConfig()
        {
            return new LoggingConfiguration
            {
                MinimumLevel = LogLevel.Debug,
                EnableConsoleOutput = true,
                EnableFileOutput = true,
                EnablePerformanceLogging = true,
                EnableClipboardLogging = true,
                EnableUserActionLogging = true,
                RetentionDays = 7
            };
        }

        /// <summary>
        /// 创建生产环境配置
        /// </summary>
        public static LoggingConfiguration CreateProductionConfig()
        {
            return new LoggingConfiguration
            {
                MinimumLevel = LogLevel.Info,
                EnableConsoleOutput = false,
                EnableFileOutput = true,
                EnablePerformanceLogging = false,
                EnableClipboardLogging = false,
                EnableUserActionLogging = true,
                RetentionDays = 30
            };
        }
    }
}