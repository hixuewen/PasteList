using PasteList.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 服务器API客户端实现
    /// </summary>
    public class ServerApiClient : IServerApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILoggerService _loggerService;
        private bool _disposed = false;

        /// <summary>
        /// 服务器配置
        /// </summary>
        public ServerSyncConfig Config { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="loggerService">日志服务</param>
        public ServerApiClient(ServerSyncConfig config, ILoggerService loggerService)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Config.ConnectionTimeoutSeconds)
            };

            // 设置默认请求头
            _httpClient.DefaultRequestHeaders.Add("X-Device-ID", Config.DeviceId);
            _httpClient.DefaultRequestHeaders.Add("X-Client-Version", "1.0.0");
        }

        /// <summary>
        /// 验证与服务器的连接
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{Config.GetApiBaseUrl()}/sync/status";
                _loggerService.LogDebug($"验证服务器连接: {url}");

                var response = await _httpClient.GetAsync(url, cancellationToken);
                var isValid = response.IsSuccessStatusCode;

                if (isValid)
                {
                    _loggerService.LogInfo($"服务器连接验证成功: {Config.ServerUrl}");
                }
                else
                {
                    _loggerService.LogWarning($"服务器连接验证失败，状态码: {response.StatusCode}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"验证服务器连接时发生错误: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 推送剪贴板数据到服务器
        /// </summary>
        public async Task<PushResult> PushItemsAsync(List<ClipboardItem> items, string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    return new PushResult(0, 0, DateTime.UtcNow);
                }

                var url = $"{Config.GetApiBaseUrl()}/sync/push";
                _loggerService.LogInfo($"推送 {items.Count} 条记录到服务器: {Config.ServerUrl}");

                // 序列化数据
                var payload = new
                {
                    DeviceId = deviceId,
                    Items = items,
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"推送失败，状态码: {response.StatusCode}";
                    _loggerService.LogError(errorMessage);
                    return new PushResult(errorMessage);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PushResultDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (result == null)
                {
                    return new PushResult("服务器返回数据格式错误");
                }

                var pushResult = new PushResult(result.PushedCount, result.SkippedCount, result.ServerTimestamp);
                _loggerService.LogInfo($"成功推送 {pushResult.PushedCount} 条记录，跳过 {pushResult.SkippedCount} 条");

                return pushResult;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"推送数据到服务器时发生错误: {ex.Message}", ex);
                return new PushResult($"推送失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从服务器拉取剪贴板数据
        /// </summary>
        public async Task<List<ClipboardItem>> PullItemsAsync(string deviceId, DateTime? lastSyncTime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{Config.GetApiBaseUrl()}/sync/pull";
                var queryParams = new List<string>
                {
                    $"deviceId={Uri.EscapeDataString(deviceId)}"
                };

                if (lastSyncTime.HasValue)
                {
                    queryParams.Add($"since={Uri.EscapeDataString(lastSyncTime.Value.ToString("o"))}");
                }

                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }

                _loggerService.LogDebug($"从服务器拉取数据: {url}");

                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"拉取失败，状态码: {response.StatusCode}";
                    _loggerService.LogError(errorMessage);
                    return new List<ClipboardItem>();
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PullResultDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (result?.Items == null)
                {
                    _loggerService.LogWarning("服务器返回数据格式错误或为空");
                    return new List<ClipboardItem>();
                }

                _loggerService.LogInfo($"成功从服务器拉取 {result.Items.Count} 条记录");
                return result.Items;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"从服务器拉取数据时发生错误: {ex.Message}", ex);
                return new List<ClipboardItem>();
            }
        }

        /// <summary>
        /// 双向同步（合并数据）
        /// </summary>
        public async Task<BidirectionalSyncResult> BidirectionalSyncAsync(List<ClipboardItem> items, string deviceId, DateTime? lastSyncTime, CancellationToken cancellationToken = default)
        {
            try
            {
                if (items == null)
                    items = new List<ClipboardItem>();

                var url = $"{Config.GetApiBaseUrl()}/sync/merge";
                _loggerService.LogInfo($"执行双向同步，推送 {items.Count} 条记录");

                var payload = new
                {
                    DeviceId = deviceId,
                    LocalItems = items,
                    LastSyncTime = lastSyncTime,
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"双向同步失败，状态码: {response.StatusCode}";
                    _loggerService.LogError(errorMessage);
                    return new BidirectionalSyncResult(errorMessage);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BidirectionalSyncResultDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (result == null)
                {
                    return new BidirectionalSyncResult("服务器返回数据格式错误");
                }

                var syncResult = new BidirectionalSyncResult(
                    result.ServerItems ?? new List<ClipboardItem>(),
                    result.ConflictsResolved,
                    result.PushedCount,
                    result.PulledCount,
                    result.ServerTimestamp
                );

                _loggerService.LogInfo($"双向同步成功，推送 {syncResult.PushedCount} 条，拉取 {syncResult.PulledCount} 条，解决冲突 {syncResult.ConflictsResolved} 个");
                return syncResult;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"双向同步时发生错误: {ex.Message}", ex);
                return new BidirectionalSyncResult($"双向同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        public async Task<ServerStatus> GetServerStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{Config.GetApiBaseUrl()}/sync/status";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new ServerStatus
                    {
                        ServerName = Config.ServerUrl,
                        Version = "Unknown",
                        ServerTime = DateTime.UtcNow,
                        IsHealthy = false,
                        ErrorMessage = $"请求失败，状态码: {response.StatusCode}"
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<ServerStatus>(responseJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (status == null)
                {
                    return new ServerStatus
                    {
                        ServerName = Config.ServerUrl,
                        Version = "Unknown",
                        ServerTime = DateTime.UtcNow,
                        IsHealthy = false,
                        ErrorMessage = "服务器返回数据格式错误"
                    };
                }

                return status;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"获取服务器状态时发生错误: {ex.Message}", ex);
                return new ServerStatus
                {
                    ServerName = Config.ServerUrl,
                    Version = "Unknown",
                    ServerTime = DateTime.UtcNow,
                    IsHealthy = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    #region DTOs for JSON serialization

    /// <summary>
    /// 推送结果DTO
    /// </summary>
    internal class PushResultDto
    {
        public bool Success { get; set; }
        public int PushedCount { get; set; }
        public int SkippedCount { get; set; }
        public DateTime? ServerTimestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 拉取结果DTO
    /// </summary>
    internal class PullResultDto
    {
        public List<ClipboardItem>? Items { get; set; }
        public DateTime? ServerTimestamp { get; set; }
    }

    /// <summary>
    /// 双向同步结果DTO
    /// </summary>
    internal class BidirectionalSyncResultDto
    {
        public bool Success { get; set; }
        public List<ClipboardItem>? ServerItems { get; set; }
        public int ConflictsResolved { get; set; }
        public int PushedCount { get; set; }
        public int PulledCount { get; set; }
        public DateTime? ServerTimestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
