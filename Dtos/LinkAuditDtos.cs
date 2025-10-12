using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SProtectAgentWeb.Api.Dtos;

public class LanzouLinkListRequest
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public class LanzouLinkRecordDto
{
    public long Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ExtractionCode { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}

public class LanzouLinkListResponse
{
    public IList<LanzouLinkRecordDto> Items { get; set; } = new List<LanzouLinkRecordDto>();
    public long Total { get; set; }
        = 0;
}

public class DeleteLanzouLinksRequest
{
    [MinLength(1)]
    public IList<long> Ids { get; set; } = new List<long>();
}

public class CardVerificationLogListRequest
{
    public string? CardKey { get; set; }
    public string? IpAddress { get; set; }
    public string? Keyword { get; set; }
    public bool? WasSuccessful { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public class CardVerificationLogRecordDto
{
    public long Id { get; set; }
    public string CardKey { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
        = 1;
    public bool WasSuccessful { get; set; }
        = false;
    public long? DownloadLinkId { get; set; }
        = null;
    public string? DownloadUrl { get; set; }
        = null;
    public string? ExtractionCode { get; set; }
        = null;
    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}

public class CardVerificationLogListResponse
{
    public IList<CardVerificationLogRecordDto> Items { get; set; } = new List<CardVerificationLogRecordDto>();
    public long Total { get; set; }
        = 0;
}

public class DeleteCardVerificationLogsRequest
{
    [MinLength(1)]
    public IList<long> Ids { get; set; } = new List<long>();
}

public class SuspiciousCardRequest
{
    public int MinSuccessCount { get; set; } = 3;
    public double StaleHours { get; set; } = 2;
}

public class SuspiciousCardRecordDto
{
    public string CardKey { get; set; } = string.Empty;
    public string? Whom { get; set; }
        = null;
    public string? CardType { get; set; }
        = null;
    public string? State { get; set; }
        = null;
    public long SuccessCount { get; set; }
        = 0;
    public DateTimeOffset FirstSuccessAt { get; set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSuccessAt { get; set; }
        = DateTimeOffset.UtcNow;
    public TimeSpan DurationSinceLast { get; set; }
        = TimeSpan.Zero;
    public DateTimeOffset? CardCreatedAt { get; set; }
        = null;
}
