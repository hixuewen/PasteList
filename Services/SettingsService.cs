using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 应用设置服务实现
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private readonly ILoggerService? _logger;
        private AppSettings _settings;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 同步设置变化事件
        /// </summary>
        public event EventHandler<bool>? SyncEnabledChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        public SettingsService(ILoggerService? logger = null)
        {
            _logger = logger;
            _settings = new AppSettings();

            // 配置文件路径 - 保存到软件所在目录
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _settingsFilePath = Path.Combine(appDir, "settings.json");

            _logger?.LogDebug($"SettingsService初始化完成, 设置文件路径: {_settingsFilePath}");
        }

        /// <summary>
        /// 是否启用同步
        /// </summary>
        public bool IsSyncEnabled
        {
            get => _settings.IsSyncEnabled;
            set
            {
                if (_settings.IsSyncEnabled != value)
                {
                    _settings.IsSyncEnabled = value;
                    _ = SaveAsync();
                    OnSyncEnabledChanged(value);
                }
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger?.LogDebug($"设置文件不存在，使用默认设置: {_settingsFilePath}");
                    return;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                if (settings != null)
                {
                    _settings = settings;
                    _logger?.LogDebug($"设置加载成功，同步状态: {_settings.IsSyncEnabled}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载设置文件时发生错误");
                // 使用默认设置
                _settings = new AppSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                await File.WriteAllTextAsync(_settingsFilePath, json);
                _logger?.LogDebug($"设置已保存到文件: {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存设置文件时发生错误");
            }
        }

        /// <summary>
        /// 触发同步设置变化事件
        /// </summary>
        private void OnSyncEnabledChanged(bool isEnabled)
        {
            SyncEnabledChanged?.Invoke(this, isEnabled);
        }

        /// <summary>
        /// 应用设置模型
        /// </summary>
        private class AppSettings
        {
            /// <summary>
            /// 是否启用同步
            /// </summary>
            public bool IsSyncEnabled { get; set; } = false;
        }
    }
}

