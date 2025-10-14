using System.ComponentModel.DataAnnotations;

namespace SProtectPlatform.Api.Models.Dto;

public sealed class CreateBindingRequest
{
    [Required]
    public string SoftwareCode { get; set; } = string.Empty;

    [Required]
    public string AuthorAccount { get; set; } = string.Empty;

    [Required]
    public string AuthorPassword { get; set; } = string.Empty;
}

public sealed record BindingListItemDto(
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

public sealed class UpdateBindingRequest
{
    [Required]
    public string AuthorAccount { get; set; } = string.Empty;

    [Required]
    public string AuthorPassword { get; set; } = string.Empty;
}
