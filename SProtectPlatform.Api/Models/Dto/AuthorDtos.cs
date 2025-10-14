using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SProtectPlatform.Api.Models.Dto;

public sealed class AuthorRegisterRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string ApiAddress { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int ApiPort { get; set; } = 5000;

    [Required]
    public string SoftwareType { get; set; } = "SP";
}

public sealed class AuthorLoginRequest
{
    [Required]
    public string Account { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthorSoftwareDto
{
    public int SoftwareId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ApiAddress { get; set; } = string.Empty;

    public int ApiPort { get; set; }

    public string SoftwareType { get; set; } = string.Empty;

    public string SoftwareCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public sealed class AuthorProfileDto
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ApiAddress { get; init; } = string.Empty;

    public int ApiPort { get; init; }

    public string SoftwareType { get; init; } = string.Empty;

    public string SoftwareCode { get; init; } = string.Empty;

    public IReadOnlyList<AuthorSoftwareDto> Softwares { get; init; } = Array.Empty<AuthorSoftwareDto>();

    public int? PrimarySoftwareId { get; init; }
}

public sealed record AuthorLoginResponseDto(
    AuthorProfileDto Profile,
    string Token,
    DateTime ExpiresAt
);

public sealed class AuthorUpdateRequest
{
    [Required]
    public int SoftwareId { get; set; }

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string ApiAddress { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int ApiPort { get; set; }

    [Required]
    public string SoftwareType { get; set; } = string.Empty;
}

public sealed record AuthorSoftwareCodeResponse(string SoftwareCode);

public class AuthorSoftwareCreateRequest
{
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string ApiAddress { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int ApiPort { get; set; }

    [Required]
    public string SoftwareType { get; set; } = "SP";
}

public sealed class AuthorSoftwareUpdateRequest : AuthorSoftwareCreateRequest
{
}
