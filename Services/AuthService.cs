using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 认证服务实现
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILoggerService? _logger;
        private readonly string _configFilePath;
        private readonly SemaphoreSlim _authLock = new SemaphoreSlim(1, 1);

        private string? _accessToken;
        private string? _refreshToken;
        private UserInfo? _currentUser;
        private DateTime _tokenExpiresAt;

        // 统一的 JSON 序列化选项
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// API服务器基础地址
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:3000/api/v1";

        /// <summary>
        /// 当前是否已登录
        /// </summary>
        public bool IsLoggedIn => _currentUser != null && !string.IsNullOrEmpty(_accessToken);

        /// <summary>
        /// 当前用户信息
        /// </summary>
        public UserInfo? CurrentUser => _currentUser;

        /// <summary>
        /// 登录状态变化事件
        /// </summary>
        public event EventHandler<bool>? LoginStateChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        public AuthService(ILoggerService? logger = null)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // 配置文件路径 - 保存到软件所在目录
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(appDir, "auth.json");
            
            _logger?.LogDebug($"AuthService初始化完成, 凭证路径: {_configFilePath}");
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        public async Task<AuthResult> RegisterAsync(string username, string email, string password)
        {
            try
            {
                _logger?.LogUserAction("尝试注册", $"用户名: {username}, 邮箱: {email}");

                var requestBody = new
                {
                    username,
                    email,
                    password,
                    deviceId = GetDeviceId()
                };

                var response = await PostAsync("/auth/register", requestBody);
                
                if (response.Success)
                {
                    await SaveCredentialsAsync(response);
                    OnLoginStateChanged(true);
                    _logger?.LogInfo($"注册成功，用户: {username}");
                    
                    // 验证保存是否成功
                    if (!File.Exists(_configFilePath))
                    {
                        _logger?.LogWarning($"注册成功但凭证文件未创建: {_configFilePath}");
                    }
                }
                else
                {
                    _logger?.LogWarning($"注册失败: {response.ErrorMessage}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注册过程中发生错误");
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = $"注册失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password)
        {
            await _authLock.WaitAsync();
            try
            {
                _logger?.LogUserAction("尝试登录", $"用户: {usernameOrEmail}");

                var requestBody = new
                {
                    username = usernameOrEmail,
                    password,
                    deviceId = GetDeviceId(),
                    rememberMe = true
                };

                var response = await PostAsync("/auth/login", requestBody);

                if (response.Success)
                {
                    await SaveCredentialsAsync(response);
                    OnLoginStateChanged(true);
                    _logger?.LogInfo($"登录成功，用户: {usernameOrEmail}");
                    
                    // 验证保存是否成功
                    if (!File.Exists(_configFilePath))
                    {
                        _logger?.LogWarning($"登录成功但凭证文件未创建: {_configFilePath}");
                    }
                }
                else
                {
                    _logger?.LogWarning($"登录失败: {response.ErrorMessage}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "登录过程中发生错误");
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = $"登录失败: {ex.Message}"
                };
            }
            finally
            {
                _authLock.Release();
            }
        }

        /// <summary>
        /// 用户注销
        /// </summary>
        public async Task<bool> LogoutAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                _logger?.LogUserAction("尝试注销", $"用户: {_currentUser?.Username}");

                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    try
                    {
                        var requestBody = new { refreshToken = _refreshToken };
                        await PostAsync("/auth/logout", requestBody);
                    }
                    catch
                    {
                        // 即使API调用失败，也继续清除本地凭证
                    }
                }

                ClearCredentials();
                OnLoginStateChanged(false);
                _logger?.LogInfo("注销成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注销过程中发生错误");
                // 即使出错也清除本地凭证
                ClearCredentials();
                OnLoginStateChanged(false);
                return false;
            }
            finally
            {
                _authLock.Release();
            }
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        public async Task<AuthResult> RefreshTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_refreshToken))
                {
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "没有可用的刷新令牌"
                    };
                }

                _logger?.LogDebug("尝试刷新令牌");

                var requestBody = new { refreshToken = _refreshToken };
                var response = await PostAsync("/auth/refresh", requestBody);
                
                if (response.Success)
                {
                    _accessToken = response.AccessToken;
                    _refreshToken = response.RefreshToken;
                    _tokenExpiresAt = DateTime.Now.AddSeconds(response.ExpiresIn);
                    
                    await SaveTokensToFileAsync();
                    _logger?.LogDebug("令牌刷新成功并已保存到文件");
                }
                else
                {
                    _logger?.LogWarning($"令牌刷新失败: {response.ErrorMessage}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "刷新令牌过程中发生错误");
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = $"刷新令牌失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 验证当前令牌是否有效
        /// </summary>
        public async Task<bool> ValidateTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var response = await _httpClient.GetAsync($"{BaseUrl}/auth/validate");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "验证令牌过程中发生错误");
                return false;
            }
        }

        /// <summary>
        /// 尝试自动登录（使用保存的凭证）
        /// </summary>
        public async Task<bool> TryAutoLoginAsync()
        {
            try
            {
                _logger?.LogDebug("尝试自动登录");

                if (!File.Exists(_configFilePath))
                {
                    _logger?.LogDebug($"没有保存的凭证: {_configFilePath}");
                    return false;
                }

                var json = await File.ReadAllTextAsync(_configFilePath);
                _logger?.LogDebug($"读取凭证文件长度: {json.Length}");
                
                var savedAuth = JsonSerializer.Deserialize<SavedAuthData>(json, _jsonOptions);

                if (savedAuth == null || string.IsNullOrEmpty(savedAuth.RefreshToken))
                {
                    _logger?.LogDebug("保存的凭证无效: 反序列化结果为空或令牌为空");
                    return false;
                }

                _refreshToken = savedAuth.RefreshToken;
                _currentUser = savedAuth.User;
                _logger?.LogDebug($"读取到凭证，用户: {_currentUser?.Username}, 准备刷新令牌");

                // 尝试刷新令牌
                var result = await RefreshTokenAsync();
                
                if (result.Success)
                {
                    OnLoginStateChanged(true);
                    _logger?.LogInfo($"自动登录成功，用户: {_currentUser?.Username}");
                    return true;
                }
                else
                {
                    // 只有当错误明确是令牌无效/过期时才清除凭证
                    // 如果是网络错误，保留凭证以便下次重试
                    if (IsTokenExpiredError(result.ErrorMessage))
                    {
                        ClearCredentials();
                        _logger?.LogWarning("自动登录失败，令牌已过期，凭证已清除");
                    }
                    else
                    {
                        // 网络错误等情况，保留凭证但清除内存中的状态
                        _refreshToken = null;
                        _currentUser = null;
                        _logger?.LogWarning($"自动登录失败（网络错误），保留凭证: {result.ErrorMessage}");
                    }
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                // 网络错误，保留凭证文件以便下次重试
                _refreshToken = null;
                _currentUser = null;
                _logger?.LogWarning($"自动登录网络错误，保留凭证: {ex.Message}");
                return false;
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, $"凭证文件JSON格式错误: {_configFilePath}");
                // JSON 格式错误说明文件损坏，可以删除
                ClearCredentials();
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "自动登录过程中发生错误");
                // 其他未知错误也保留凭证
                _refreshToken = null;
                _currentUser = null;
                return false;
            }
        }

        /// <summary>
        /// 判断错误是否是令牌过期/无效错误
        /// </summary>
        private bool IsTokenExpiredError(string? errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return false;

            // 常见的令牌过期/无效错误关键词
            var tokenErrorKeywords = new[] { 
                "token", "expired", "invalid", "unauthorized", 
                "令牌", "过期", "无效", "未授权", "401"
            };

            var lowerMessage = errorMessage.ToLowerInvariant();
            return tokenErrorKeywords.Any(keyword => lowerMessage.Contains(keyword.ToLowerInvariant()));
        }

        /// <summary>
        /// 获取访问令牌（异步版本）
        /// </summary>
        public async Task<string?> GetAccessTokenAsync()
        {
            // 检查令牌是否即将过期，如果是则尝试刷新
            if (_tokenExpiresAt != default && DateTime.Now.AddMinutes(5) >= _tokenExpiresAt)
            {
                _logger?.LogDebug($"令牌即将过期 (过期时间: {_tokenExpiresAt}), 尝试刷新");
                try
                {
                    var result = await RefreshTokenAsync();
                    if (!result.Success)
                    {
                        _logger?.LogWarning("令牌刷新失败，清除凭证");
                        ClearCredentials();
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "令牌刷新时发生异常");
                    ClearCredentials();
                    return null;
                }
            }
            return _accessToken;
        }

        /// <summary>
        /// 获取访问令牌（同步版本，向后兼容）
        /// </summary>
        public string? GetAccessToken()
        {
            // 在后台线程执行异步操作，避免阻塞UI线程
            var task = Task.Run(async () => await GetAccessTokenAsync());
            return task.Result;
        }

        /// <summary>
        /// 发送POST请求
        /// </summary>
        private async Task<AuthResult> PostAsync(string endpoint, object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}{endpoint}", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger?.LogDebug($"API响应: {endpoint} - {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody, _jsonOptions);

                if (apiResponse?.Data != null)
                {
                    // 优先使用扁平结构的令牌（刷新接口），如果没有则使用嵌套结构（登录接口）
                    var accessToken = apiResponse.Data.AccessToken ?? apiResponse.Data.Token?.AccessToken;
                    var refreshToken = apiResponse.Data.RefreshToken ?? apiResponse.Data.Token?.RefreshToken;
                    var expiresIn = apiResponse.Data.ExpiresIn ?? apiResponse.Data.Token?.ExpiresIn ?? 3600;
                    
                    return new AuthResult
                    {
                        Success = true,
                        User = apiResponse.Data.User,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresIn = expiresIn
                    };
                }
            }

            // 解析错误响应
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, _jsonOptions);
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = errorResponse?.Error?.Message ?? "请求失败"
                };
            }
            catch
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = $"请求失败: {response.StatusCode}"
                };
            }
        }

        /// <summary>
        /// 保存凭证到内存和文件
        /// </summary>
        private async Task SaveCredentialsAsync(AuthResult result)
        {
            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            _currentUser = result.User;
            _tokenExpiresAt = DateTime.Now.AddSeconds(result.ExpiresIn);

            // 保存凭证到文件以支持下次启动时自动登录
            await SaveTokensToFileAsync();
        }

        /// <summary>
        /// 保存令牌到文件
        /// </summary>
        private async Task SaveTokensToFileAsync()
        {
            try
            {
                if (_refreshToken == null)
                {
                    _logger?.LogDebug("没有凭证需要保存: RefreshToken为null");
                    return;
                }

                var savedAuth = new SavedAuthData
                {
                    RefreshToken = _refreshToken,
                    User = _currentUser
                };

                var json = JsonSerializer.Serialize(savedAuth, _jsonOptions);
                await File.WriteAllTextAsync(_configFilePath, json);
                _logger?.LogDebug($"凭证已保存到文件: {_configFilePath}, 长度: {json.Length}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"保存凭证时发生错误: {_configFilePath}");
            }
        }


        /// <summary>
        /// 清除凭证
        /// </summary>
        private void ClearCredentials()
        {
            _accessToken = null;
            _refreshToken = null;
            _currentUser = null;
            _tokenExpiresAt = default;

            try
            {
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                    _logger?.LogDebug($"凭证文件已删除: {_configFilePath}");
                }
                else
                {
                     _logger?.LogDebug($"尝试删除凭证文件但文件不存在: {_configFilePath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除凭证文件时发生错误");
            }
        }

        /// <summary>
        /// 获取设备ID
        /// </summary>
        private string GetDeviceId()
        {
            var deviceId = Environment.MachineName + "_" + Environment.UserName;
            // _logger?.LogDebug($"获取设备ID: {deviceId}");
            return deviceId;
        }

        /// <summary>
        /// 上传剪贴板项到服务器
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>上传结果</returns>
        public async Task<UploadResult> UploadClipboardItemAsync(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                {
                    return new UploadResult
                    {
                        Success = false,
                        ErrorMessage = "内容不能为空"
                    };
                }

                // 检查是否已登录
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new UploadResult
                    {
                        Success = false,
                        ErrorMessage = "未登录，请先登录"
                    };
                }

                _logger?.LogUserAction("上传剪贴板项", $"内容长度: {content.Length}");

                var requestBody = new
                {
                    content,
                    deviceId = GetDeviceId(),
                    createdAt = DateTime.UtcNow.ToString("o")
                };

                var json = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.PostAsync($"{BaseUrl}/clipboard/items", httpContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger?.LogDebug($"上传响应: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInfo("剪贴板项上传成功");
                    return new UploadResult
                    {
                        Success = true,
                        Message = "上传成功"
                    };
                }
                else
                {
                    // 解析错误响应
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, _jsonOptions);
                        var errorMessage = errorResponse?.Error?.Message ?? $"上传失败: {response.StatusCode}";
                        _logger?.LogWarning($"上传失败: {errorMessage}");
                        return new UploadResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                    }
                    catch
                    {
                        return new UploadResult
                        {
                            Success = false,
                            ErrorMessage = $"上传失败: {response.StatusCode}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上传剪贴板项时发生错误");
                return new UploadResult
                {
                    Success = false,
                    ErrorMessage = $"上传失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 删除服务器上的剪贴板项
        /// </summary>
        /// <param name="serverId">服务器端ID</param>
        /// <returns>删除结果</returns>
        public async Task<DeleteResult> DeleteClipboardItemAsync(int serverId)
        {
            try
            {
                // 检查是否已登录
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new DeleteResult
                    {
                        Success = false,
                        ErrorMessage = "未登录，请先登录"
                    };
                }

                _logger?.LogUserAction("删除服务器剪贴板项", $"ServerId: {serverId}");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/clipboard/items/{serverId}");
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger?.LogDebug($"删除响应: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInfo($"服务器剪贴板项删除成功，ServerId: {serverId}");
                    return new DeleteResult
                    {
                        Success = true,
                        Message = "删除成功"
                    };
                }
                else
                {
                    // 解析错误响应
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, _jsonOptions);
                        var errorMessage = errorResponse?.Error?.Message ?? $"删除失败: {response.StatusCode}";
                        _logger?.LogWarning($"删除失败: {errorMessage}");
                        return new DeleteResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        };
                    }
                    catch
                    {
                        return new DeleteResult
                        {
                            Success = false,
                            ErrorMessage = $"删除失败: {response.StatusCode}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除服务器剪贴板项时发生错误");
                return new DeleteResult
                {
                    Success = false,
                    ErrorMessage = $"删除失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 根据内容查找服务器上剪贴板项的ID
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>服务器端ID，如果未找到则返回null</returns>
        public async Task<int?> FindServerItemIdByContentAsync(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                {
                    return null;
                }

                // 检查是否已登录
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger?.LogWarning("查找服务器剪贴板项失败：未登录");
                    return null;
                }

                _logger?.LogDebug($"正在查找服务器剪贴板项，内容长度: {content.Length}");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                // 获取服务器上的剪贴板项目列表，使用较大的 pageSize 以获取更多项目
                var response = await _httpClient.GetAsync($"{BaseUrl}/clipboard/items?pageSize=100");
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var listResponse = JsonSerializer.Deserialize<ClipboardListResponse>(responseBody, _jsonOptions);
                    
                    if (listResponse?.Data?.Items != null)
                    {
                        // 根据内容匹配找到对应的服务器端ID
                        foreach (var item in listResponse.Data.Items)
                        {
                            if (item.Content == content)
                            {
                                _logger?.LogDebug($"找到匹配的服务器剪贴板项，ServerId: {item.Id}");
                                return item.Id;
                            }
                        }
                    }
                    
                    _logger?.LogDebug("未找到匹配的服务器剪贴板项");
                    return null;
                }
                else
                {
                    _logger?.LogWarning($"获取服务器剪贴板列表失败: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查找服务器剪贴板项时发生错误");
                return null;
            }
        }

        /// <summary>
        /// 根据内容删除服务器上的剪贴板项（先查找ID再删除）
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>删除结果</returns>
        public async Task<DeleteResult> DeleteClipboardItemByContentAsync(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                {
                    return new DeleteResult
                    {
                        Success = false,
                        ErrorMessage = "内容不能为空"
                    };
                }

                // 检查是否已登录
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new DeleteResult
                    {
                        Success = false,
                        ErrorMessage = "未登录，请先登录"
                    };
                }

                _logger?.LogUserAction("根据内容删除服务器剪贴板项", $"内容长度: {content.Length}");

                // 先查找服务器端ID
                var serverId = await FindServerItemIdByContentAsync(content);
                
                if (!serverId.HasValue)
                {
                    _logger?.LogDebug("服务器上未找到匹配的剪贴板项，跳过服务器删除");
                    return new DeleteResult
                    {
                        Success = true,
                        Message = "服务器上未找到匹配记录"
                    };
                }

                // 调用删除接口
                return await DeleteClipboardItemAsync(serverId.Value);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "根据内容删除服务器剪贴板项时发生错误");
                return new DeleteResult
                {
                    Success = false,
                    ErrorMessage = $"删除失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取服务器上当前用户的所有剪贴板项
        /// </summary>
        /// <returns>同步结果，包含服务器上的所有剪贴板项</returns>
        public async Task<SyncResult> GetAllClipboardItemsAsync()
        {
            try
            {
                // 检查是否已登录
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new SyncResult
                    {
                        Success = false,
                        ErrorMessage = "未登录，请先登录"
                    };
                }

                _logger?.LogUserAction("获取服务器剪贴板项", "开始同步");

                var allItems = new List<ServerClipboardItem>();
                int currentPage = 1;
                int pageSize = 100;
                int totalPages = 1;

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // 分页获取所有数据
                do
                {
                    var response = await _httpClient.GetAsync($"{BaseUrl}/clipboard/items?page={currentPage}&pageSize={pageSize}");
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var listResponse = JsonSerializer.Deserialize<ClipboardListResponse>(responseBody, _jsonOptions);

                        if (listResponse?.Data?.Items != null)
                        {
                            foreach (var item in listResponse.Data.Items)
                            {
                                allItems.Add(new ServerClipboardItem
                                {
                                    Id = item.Id,
                                    Content = item.Content ?? string.Empty,
                                    DeviceId = item.DeviceId,
                                    CreatedAt = ParseDateTime(item.CreatedAt)
                                });
                            }

                            // 获取分页信息
                            if (listResponse.Data.Pagination != null)
                            {
                                totalPages = listResponse.Data.Pagination.TotalPages;
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogWarning($"获取服务器剪贴板列表失败: {response.StatusCode}");
                        return new SyncResult
                        {
                            Success = false,
                            ErrorMessage = $"获取服务器数据失败: {response.StatusCode}"
                        };
                    }

                    currentPage++;
                } while (currentPage <= totalPages);

                _logger?.LogInfo($"成功获取服务器剪贴板项，共 {allItems.Count} 条");

                return new SyncResult
                {
                    Success = true,
                    Message = $"成功获取 {allItems.Count} 条记录",
                    Items = allItems
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取服务器剪贴板项时发生错误");
                return new SyncResult
                {
                    Success = false,
                    ErrorMessage = $"获取失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 解析日期时间字符串
        /// </summary>
        private DateTime? ParseDateTime(string? dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
                return null;

            if (DateTime.TryParse(dateTimeString, out var result))
                return result;

            return null;
        }

        /// <summary>
        /// 触发登录状态变化事件
        /// </summary>
        private void OnLoginStateChanged(bool isLoggedIn)
        {
            LoginStateChanged?.Invoke(this, isLoggedIn);
        }

        #region JSON模型类

        private class ApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public ApiData? Data { get; set; }
        }

        private class ApiData
        {
            public UserInfo? User { get; set; }
            public TokenInfo? Token { get; set; }
            
            // 支持刷新令牌接口返回的扁平结构
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public int? ExpiresIn { get; set; }
        }

        private class ClipboardItemData
        {
            public int Id { get; set; }
            public string? Content { get; set; }
            public string? DeviceId { get; set; }
            public string? CreatedAt { get; set; }
        }

        private class ClipboardListResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public ClipboardListData? Data { get; set; }
        }

        private class ClipboardListData
        {
            public List<ClipboardItemData>? Items { get; set; }
            public PaginationData? Pagination { get; set; }
        }

        private class PaginationData
        {
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
        }

        private class TokenInfo
        {
            public string? AccessToken { get; set; }
            public string? RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string? TokenType { get; set; }
        }

        private class ApiErrorResponse
        {
            public bool Success { get; set; }
            public ApiError? Error { get; set; }
        }

        private class ApiError
        {
            public string? Code { get; set; }
            public string? Message { get; set; }
        }

        public class SavedAuthData
        {
            public string? RefreshToken { get; set; }
            public UserInfo? User { get; set; }
        }

        #endregion
    }
}
