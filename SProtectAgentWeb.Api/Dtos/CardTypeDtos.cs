using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 卡密类型列表请求参数。
/// </summary>
public class CardTypeListRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;
}

/// <summary>
/// 卡密类型列表响应。
/// </summary>
public class CardTypeListResponse
{
    /// <summary>
    /// 卡密类型集合。
    /// </summary>
    public IList<CardType> Items { get; set; } = new List<CardType>();
}

/// <summary>
/// 根据名称查询卡密类型请求。
/// </summary>
public class GetCardTypeByNameRequest
{
    /// <summary>
    /// 软件位标识。
    /// </summary>
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 卡密类型名称。
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 卡密类型详情响应。
/// </summary>
public class CardTypeResponse
{
    /// <summary>
    /// 卡密类型详情。
    /// </summary>
    public CardType? CardType { get; set; }
}
