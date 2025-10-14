using System;

namespace SProtectPlatform.Api.Models;

public sealed class WeChatMessageLog
{
    public int Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
