using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 软件列表请求参数。
/// </summary>
public class SoftwareListRequest
{
    /// <summary>
    /// 登录用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// 软件列表响应。
/// </summary>
public class SoftwareListResponse
{
    /// <summary>
    /// 软件位集合。
    /// </summary>
    public IList<SoftwareAgentInfo> Softwares { get; set; } = new List<SoftwareAgentInfo>();
}

/// <summary>
/// 软件详情请求参数。
/// </summary>
public class SoftwareInfoRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;
}

/// <summary>
/// 软件详情响应。
/// </summary>
public class SoftwareInfoResponse
{
    /// <summary>
    /// 软件位详情。
    /// </summary>
    public SoftwareAgentInfo? Software { get; set; }
}
