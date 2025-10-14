using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SProtectPlatform.Api.Models.Dto;

public sealed record WeChatBindRequest
{
    [Required(ErrorMessage = "jsCode 必填")] public string JsCode { get; init; } = string.Empty;

    [StringLength(191, ErrorMessage = "昵称长度不能超过 191 个字符")]
    public string? Nickname { get; init; }
}

public sealed record WeChatBindResponse(string OpenId, string? UnionId, string UserType, int UserId, string? Nickname);

public sealed record WeChatBindingDto(string OpenId, string? UnionId, string UserType, int UserId, string? Nickname);

public sealed record WeChatNotificationRequest
{
    [Required(ErrorMessage = "模板类型必填")] public string Template { get; init; } = string.Empty;
    [Required(ErrorMessage = "数据必填")] public Dictionary<string, string> Data { get; init; } = new();
    public string? Page { get; init; }
    public string? RecipientType { get; init; }
    public int? RecipientId { get; init; }
};

public sealed record WeChatNotificationResultDto(bool Success, int ErrorCode, string? ErrorMessage);

public static class WeChatTemplateKeys
{
    public const string InstantCommunication = "instant";
    public const string BlacklistAlert = "blacklist";
    public const string SettlementNotice = "settlement";

    public static bool IsKnown(string value) => value is InstantCommunication or BlacklistAlert or SettlementNotice;
}

public sealed record WeChatTemplateConfigDto(
    string? InstantCommunication,
    string? BlacklistAlert,
    string? SettlementNotice,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PreviewData)
{
    public IReadOnlyList<string> ToArray() => new[] { InstantCommunication, BlacklistAlert, SettlementNotice }
        .Where(static id => !string.IsNullOrWhiteSpace(id))
        .Select(static id => id!)
        .ToArray();
}
