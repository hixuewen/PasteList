using System;
using System.IO;
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
            
            // 配置文件路径
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PasteList");
            Directory.CreateDirectory(appDataPath);
            _configFilePath = Path.Combine(appDataPath, "auth.json");
            
            _logger?.LogDebug("AuthService初始化完成");
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
                    rememberMe = false
                };

                var response = await PostAsync("/auth/login", requestBody);

                if (response.Success)
                {
                    await SaveCredentialsAsync(response);
                    OnLoginStateChanged(true);
                    _logger?.LogInfo($"登录成功，用户: {usernameOrEmail}");
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
                    
                    // 不再保存令牌到文件
                    _logger?.LogDebug("令牌刷新成功");
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
        /// 获取访问令牌（异步版本）
        /// </summary>
        public async Task<string?> GetAccessTokenAsync()
        {
            // 检查令牌是否即将过期，如果是则尝试刷新
            if (_tokenExpiresAt != default && DateTime.Now.AddMinutes(5) >= _tokenExpiresAt)
            {
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
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data != null)
                {
                    return new AuthResult
                    {
                        Success = true,
                        User = apiResponse.Data.User,
                        AccessToken = apiResponse.Data.Token?.AccessToken,
                        RefreshToken = apiResponse.Data.Token?.RefreshToken,
                        ExpiresIn = apiResponse.Data.Token?.ExpiresIn ?? 3600
                    };
                }
            }

            // 解析错误响应
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
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
        /// 保存凭证（仅保存在内存中，不保存到文件）
        /// </summary>
        private async Task SaveCredentialsAsync(AuthResult result)
        {
            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            _currentUser = result.User;
            _tokenExpiresAt = DateTime.Now.AddSeconds(result.ExpiresIn);
            
            // 不再保存凭证到文件
            await Task.CompletedTask;
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
                    _logger?.LogDebug("凭证文件已删除");
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
            return Environment.MachineName + "_" + Environment.UserName;
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

        #endregion
    }
}
