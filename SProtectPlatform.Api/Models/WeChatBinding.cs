using System;

namespace SProtectPlatform.Api.Models;

public sealed class WeChatBinding
{
    public int Id { get; set; }
    public string UserType { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string? UnionId { get; set; }
    public string? Nickname { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
