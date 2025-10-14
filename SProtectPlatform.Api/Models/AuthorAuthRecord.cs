using System.Collections.Generic;

namespace SProtectPlatform.Api.Models;

public sealed class AuthorAuthRecord
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ApiAddress { get; set; } = string.Empty;

    public int ApiPort { get; set; }

    public string SoftwareType { get; set; } = string.Empty;

    public string SoftwareCode { get; set; } = string.Empty;

    public List<AuthorSoftware> Softwares { get; set; } = new();
}
