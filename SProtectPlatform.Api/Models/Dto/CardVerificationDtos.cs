using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SProtectPlatform.Api.Models.Dto;

public sealed class CardVerificationRequestDto
{
    [Required]
    public string CardKey { get; set; } = string.Empty;

    [Required]
    public string SoftwareCode { get; set; } = string.Empty;

    [Required]
    public string Software { get; set; } = string.Empty;

    public string? AgentAccount { get; set; }
        = null;
}

public sealed class CardVerificationResultDto
{
    public string CardKey { get; set; } = string.Empty;

    public bool VerificationPassed { get; set; }
        = false;

    public long AttemptNumber { get; set; }
        = 0;

    public long? ExpiresAt { get; set; }
        = null;

    public DownloadLinkDto? Download { get; set; }
        = null;

    public IReadOnlyList<DownloadLinkDto> DownloadHistory { get; set; }
        = Array.Empty<DownloadLinkDto>();

    public bool HasReachedLinkLimit { get; set; }
        = false;

    public int RemainingLinkQuota { get; set; }
        = 0;

    public string Message { get; set; } = string.Empty;
}

public sealed class DownloadLinkDto
{
    public long LinkId { get; set; }
        = 0;

    public string Url { get; set; } = string.Empty;

    public string ExtractionCode { get; set; } = string.Empty;

    public long AssignedAt { get; set; }
        = 0;

    public bool IsNew { get; set; }
        = false;
}
