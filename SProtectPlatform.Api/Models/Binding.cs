using System;

namespace SProtectPlatform.Api.Models;

public sealed class Binding
{
    public int Id { get; set; }

    public int AgentId { get; set; }

    public int AuthorId { get; set; }

    public string AuthorAccount { get; set; } = string.Empty;

    public string EncryptedAuthorPassword { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
