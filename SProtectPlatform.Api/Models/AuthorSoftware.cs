using System;

namespace SProtectPlatform.Api.Models;

public sealed class AuthorSoftware
{
    public int Id { get; set; }

    public int AuthorId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ApiAddress { get; set; } = string.Empty;

    public int ApiPort { get; set; }

    public string SoftwareType { get; set; } = string.Empty;

    public string SoftwareCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
