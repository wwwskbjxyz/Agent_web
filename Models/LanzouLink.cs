using System;

namespace SProtectAgentWeb.Api.Models;

public sealed class LanzouLinkInfo
{
    public long Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ExtractionCode { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public sealed class LanzouLinkRecord
{
    public long Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string ExtractionCode { get; init; } = string.Empty;
    public string RawContent { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
