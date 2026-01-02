using System;
using System.Threading.Tasks;

namespace PasteList.Services
{
    /// <summary>
    /// 用户认证响应模型
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 用户信息
        /// </summary>
        public UserInfo? User { get; set; }

        /// <summary>
        /// 访问令牌
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// 令牌过期时间（秒）
        /// </summary>
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// 用户信息模型
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>
    /// 认证服务接口
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// 当前是否已登录
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 当前用户信息
        /// </summary>
        UserInfo? CurrentUser { get; }

        /// <summary>
        /// 登录状态变化事件
        /// </summary>
        event EventHandler<bool>? LoginStateChanged;

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="email">邮箱</param>
        /// <param name="password">密码</param>
        /// <returns>认证结果</returns>
        Task<AuthResult> RegisterAsync(string username, string email, string password);

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="usernameOrEmail">用户名或邮箱</param>
        /// <param name="password">密码</param>
        /// <returns>认证结果</returns>
        Task<AuthResult> LoginAsync(string usernameOrEmail, string password);

        /// <summary>
        /// 用户注销
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> LogoutAsync();

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <returns>认证结果</returns>
        Task<AuthResult> RefreshTokenAsync();

        /// <summary>
        /// 验证当前令牌是否有效
        /// </summary>
        /// <returns>是否有效</returns>
        Task<bool> ValidateTokenAsync();

        /// <summary>
        /// 尝试自动登录（使用保存的凭证）
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> TryAutoLoginAsync();

        /// <summary>
        /// 获取访问令牌（用于API调用）
        /// </summary>
        /// <returns>访问令牌，如果未登录则返回null</returns>
        string? GetAccessToken();

        /// <summary>
        /// 上传剪贴板项到服务器
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>上传结果</returns>
        Task<UploadResult> UploadClipboardItemAsync(string content);

        /// <summary>
        /// 删除服务器上的剪贴板项
        /// </summary>
        /// <param name="serverId">服务器端ID</param>
        /// <returns>删除结果</returns>
        Task<DeleteResult> DeleteClipboardItemAsync(int serverId);

        /// <summary>
        /// 根据内容查找服务器上剪贴板项的ID
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>服务器端ID，如果未找到则返回null</returns>
        Task<int?> FindServerItemIdByContentAsync(string content);

        /// <summary>
        /// 根据内容删除服务器上的剪贴板项（先查找ID再删除）
        /// </summary>
        /// <param name="content">剪贴板内容</param>
        /// <returns>删除结果</returns>
        Task<DeleteResult> DeleteClipboardItemByContentAsync(string content);
    }

    /// <summary>
    /// 上传结果模型
    /// </summary>
    public class UploadResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 成功消息
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// 删除结果模型
    /// </summary>
    public class DeleteResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 成功消息
        /// </summary>
        public string? Message { get; set; }
    }
}
