using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 登录请求参数。
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 登录用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码。
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 登录成功响应。
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// 登录用户名。
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 可访问的软件位列表。
    /// </summary>
    public IList<string> SoftwareList { get; set; } = new List<string>();

    /// <summary>
    /// 登录成功后颁发的访问令牌。
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 访问令牌过期时间（UTC）。
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
        = DateTimeOffset.UtcNow.AddHours(2);

    /// <summary>
    /// 是否拥有超级权限。
    /// </summary>
    public bool IsSuper { get; set; }
}

/// <summary>
/// 修改密码请求。
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// 登录用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 原密码。
    /// </summary>
    [Required]
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>
    /// 新密码。
    /// </summary>
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 刷新用户信息响应。
/// </summary>
public class RefreshUserInfoResponse
{
    /// <summary>
    /// 登录用户名。
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 可访问的软件位列表。
    /// </summary>
    public IList<string> SoftwareList { get; set; } = new List<string>();

    /// <summary>
    /// 每个软件位对应的代理详细信息。
    /// </summary>
    public IDictionary<string, Agent> SoftwareAgentInfo { get; set; } = new Dictionary<string, Agent>();

    /// <summary>
    /// 若刷新时重新颁发令牌，则返回新的访问令牌。
    /// </summary>
    public string? Token { get; set; }
        = null;

    /// <summary>
    /// 当前访问令牌的过期时间（UTC）。
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }
        = null;

    /// <summary>
    /// 是否拥有超级权限。
    /// </summary>
    public bool IsSuper { get; set; }
}

