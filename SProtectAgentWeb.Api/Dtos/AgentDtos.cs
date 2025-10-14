using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 获取代理信息的请求参数。
/// </summary>
public class AgentInfoRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;
}

/// <summary>
/// 代理信息响应。
/// </summary>
public class AgentInfoResponse
{
    /// <summary>
    /// 当前代理信息。
    /// </summary>
    public Agent? Agent { get; set; }

    /// <summary>
    /// 当前代理拥有的权限列表。
    /// </summary>
    public IList<string> Permissions { get; set; } = new List<string>();

    /// <summary>
    /// 代理统计信息。
    /// </summary>
    public AgentStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 代理统计信息。
/// </summary>
public class AgentStatistics
{
    /// <summary>
    /// 卡密总数。
    /// </summary>
    public long TotalCards { get; set; }

    /// <summary>
    /// 启用中的卡密数量。
    /// </summary>
    public long ActiveCards { get; set; }

    /// <summary>
    /// 已使用的卡密数量。
    /// </summary>
    public long UsedCards { get; set; }

    /// <summary>
    /// 已过期的卡密数量。
    /// </summary>
    public long ExpiredCards { get; set; }

    /// <summary>
    /// 子代理数量。
    /// </summary>
    public long SubAgents { get; set; }
}

/// <summary>
/// 子代理列表查询请求。
/// </summary>
public class SubAgentListRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 搜索类型，0-用户名精准匹配，1-用户名模糊搜索（默认）。
    /// </summary>
    public int SearchType { get; set; }

    /// <summary>
    /// 搜索关键字。
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 页码，从 1 开始。
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页数量。
    /// </summary>
    public int Limit { get; set; } = 20;
}

/// <summary>
/// 子代理列表响应。
/// </summary>
public class SubAgentListResponse
{
    /// <summary>
    /// 子代理数据集合。
    /// </summary>
    public IList<object> Data { get; set; } = new List<object>();

    /// <summary>
    /// 总数。
    /// </summary>
    public int Total { get; set; }
}

/// <summary>
/// 更新子代理密码请求。
/// </summary>
public class UpdateAgentPasswordRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 新密码。
    /// </summary>
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 修改代理状态请求。
/// </summary>
public class ModifyAgentStatusRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 需要操作的子代理用户名列表。
    /// </summary>
    [Required]
    public IList<string> Username { get; set; } = new List<string>();
}

/// <summary>
/// 更新代理备注请求。
/// </summary>
public class UpdateAgentRemarkRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 新的备注内容。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}

/// <summary>
/// 创建子代理请求。
/// </summary>
public class CreateAgentRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码。
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 初始余额。
    /// </summary>
    public double InitialBalance { get; set; }

    /// <summary>
    /// 初始时间库存。
    /// </summary>
    public long InitialTimeStock { get; set; }

    /// <summary>
    /// 备注信息。
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 授权可制卡的卡密类型列表。
    /// </summary>
    public IList<string> CardTypes { get; set; } = new List<string>();

    /// <summary>
    /// 自身售价折扣，默认为 100%。
    /// </summary>
    public double Parities { get; set; } = 100.0;

    /// <summary>
    /// 下级折扣上限，默认为 100%。
    /// </summary>
    public double TotalParities { get; set; } = 100.0;
}

/// <summary>
/// 删除子代理请求。
/// </summary>
public class DeleteAgentRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 需要删除的子代理用户名列表。
    /// </summary>
    [Required]
    public IList<string> Username { get; set; } = new List<string>();
}

/// <summary>
/// 子代理充值请求。
/// </summary>
public class AddMoneyRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 充值余额数量。
    /// </summary>
    public double Balance { get; set; }

    /// <summary>
    /// 充值的时间库存数量。
    /// </summary>
    public long TimeStock { get; set; }
}

/// <summary>
/// 代理制卡类型响应。
/// </summary>
public class AgentCardTypeResponse
{
    /// <summary>
    /// 可制卡的卡密类型名称集合。
    /// </summary>
    public IList<string> CardTypes { get; set; } = new List<string>();
}

/// <summary>
/// 配置代理制卡类型请求。
/// </summary>
public class SetAgentCardTypeRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 可制卡的卡密类型名称集合。
    /// </summary>
    public IList<string> CardTypes { get; set; } = new List<string>();
}

/// <summary>
/// 代理目标请求，用于查询特定子代理。
/// </summary>
public class AgentTargetRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 子代理用户名。
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;
}
