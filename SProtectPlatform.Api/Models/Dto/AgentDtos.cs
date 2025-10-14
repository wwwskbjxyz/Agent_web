using System.ComponentModel.DataAnnotations;

namespace SProtectPlatform.Api.Models.Dto;

public sealed class AgentRegisterRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string SoftwareCode { get; set; } = string.Empty;

    [Required]
    public string AuthorAccount { get; set; } = string.Empty;

    [Required]
    public string AuthorPassword { get; set; } = string.Empty;
}

public sealed class AgentLoginRequest
{
    [Required]
    public string Account { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record AgentWeChatLoginRequest(
    [Required(ErrorMessage = "jsCode 必填")] string JsCode
);

public sealed record AgentProfileDto(
    string Username,
    string Email,
    string DisplayName
);

public sealed record AgentLoginResponseDto(
    AgentProfileDto Agent,
    string Token,
    DateTime ExpiresAt,
    IReadOnlyCollection<BindingSummaryDto> Bindings
);

public sealed record BindingSummaryDto(
    int BindingId,
    int AuthorSoftwareId,
    string SoftwareCode,
    string SoftwareType,
    string AuthorDisplayName,
    string AuthorEmail,
    string ApiAddress,
    int ApiPort,
    string AuthorAccount,
    string AuthorPassword
);
