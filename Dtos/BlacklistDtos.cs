using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 黑名单查询请求参数。
/// </summary>
public class BlacklistQueryRequest
{
    public int? Type { get; set; }
}

/// <summary>
/// 黑名单日志查询请求。
/// </summary>
public class BlacklistLogQueryRequest
{
    /// <summary>返回的最大条数，默认 200，最大 1000。</summary>
    public int Limit { get; set; } = 200;
}

/// <summary>
/// 黑名单机器码新增请求。
/// </summary>
public class BlacklistUpsertRequest
{
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>类型，默认为 2（机器码）。</summary>
    public int Type { get; set; } = 2;

    public string? Remarks { get; set; }
}

/// <summary>
/// 黑名单机器码删除请求。
/// </summary>
public class BlacklistDeleteRequest
{
    [Required]
    public IList<string> Values { get; set; } = new List<string>();
}

/// <summary>
/// 黑名单机器码响应。
/// </summary>
public class BlacklistMachineResponse
{
    public IList<BlacklistEntry> Items { get; set; } = new List<BlacklistEntry>();
}

/// <summary>
/// 黑名单日志响应。
/// </summary>
public class BlacklistLogResponse
{
    public IList<BlacklistLogEntry> Items { get; set; } = new List<BlacklistLogEntry>();
}
