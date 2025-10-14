using System;

namespace SProtectPlatform.Api.Models;

public sealed class AllowedOrigin
{
    public int Id { get; set; }

    public string Origin { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
