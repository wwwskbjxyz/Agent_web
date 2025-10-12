using System;
using System.Collections.Generic;

namespace SProtectAgentWeb.Api.Dtos;

public class CardVerificationRequest
{
    public string CardKey { get; set; } = string.Empty;
}

public class CardVerificationResult
{
    public string CardKey { get; set; } = string.Empty;
    public bool VerificationPassed { get; set; }
    public long AttemptNumber { get; set; }
    public long? ExpiresAt { get; set; }
    public DownloadLinkDto? Download { get; set; }
    public IReadOnlyList<DownloadLinkDto> DownloadHistory { get; set; } = Array.Empty<DownloadLinkDto>();
    public bool HasReachedLinkLimit { get; set; }

    public int RemainingLinkQuota { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class DownloadLinkDto
{
    public long LinkId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ExtractionCode { get; set; } = string.Empty;
    public long AssignedAt { get; set; }
    public bool IsNew { get; set; }
}
