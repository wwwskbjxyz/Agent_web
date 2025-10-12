using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public class LinkAuditService
{
    private const string TargetSoftware = "炫舞";

    private readonly LanzouLinkService _lanzouLinkService;
    private readonly CardVerificationRepository _cardVerificationRepository;
    private readonly DatabaseManager _databaseManager;
    private readonly ILogger<LinkAuditService> _logger;

    public LinkAuditService(
        LanzouLinkService lanzouLinkService,
        CardVerificationRepository cardVerificationRepository,
        DatabaseManager databaseManager,
        ILogger<LinkAuditService> logger)
    {
        _lanzouLinkService = lanzouLinkService;
        _cardVerificationRepository = cardVerificationRepository;
        _databaseManager = databaseManager;
        _logger = logger;
    }

    public async Task<LanzouLinkListResponse> GetLanzouLinksAsync(LanzouLinkListRequest request, CancellationToken cancellationToken)
    {
        var (items, total) = await _lanzouLinkService.QueryLinksAsync(request.Keyword, request.Page, request.Limit, cancellationToken);
        var response = new LanzouLinkListResponse
        {
            Total = total,
            Items = items
                .Select(item => new LanzouLinkRecordDto
                {
                    Id = item.Id,
                    Url = item.Url,
                    ExtractionCode = item.ExtractionCode,
                    RawContent = item.RawContent,
                    CreatedAt = item.CreatedAt
                })
                .ToList()
        };
        return response;
    }

    public async Task<int> DeleteLanzouLinksAsync(DeleteLanzouLinksRequest request, CancellationToken cancellationToken)
    {
        return await _lanzouLinkService.DeleteLinksAsync(request.Ids, cancellationToken);
    }

    public async Task<CardVerificationLogListResponse> GetVerificationLogsAsync(CardVerificationLogListRequest request, CancellationToken cancellationToken)
    {
        var query = new CardVerificationLogQuery
        {
            CardKey = Normalize(request.CardKey),
            IpAddress = Normalize(request.IpAddress),
            Keyword = Normalize(request.Keyword),
            WasSuccessful = request.WasSuccessful,
            StartTime = ParseTime(request.StartTime),
            EndTime = ParseTime(request.EndTime),
            Page = request.Page,
            PageSize = request.Limit
        };

        var (items, total) = await _cardVerificationRepository.QueryLogsAsync(query, cancellationToken);
        var response = new CardVerificationLogListResponse
        {
            Total = total,
            Items = items
                .Select(item => new CardVerificationLogRecordDto
                {
                    Id = item.Id,
                    CardKey = item.CardKey,
                    IpAddress = item.IpAddress,
                    AttemptNumber = item.AttemptNumber,
                    WasSuccessful = item.WasSuccessful,
                    DownloadLinkId = item.DownloadLinkId,
                    DownloadUrl = item.DownloadUrl,
                    ExtractionCode = item.ExtractionCode,
                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc))
                })
                .ToList()
        };
        return response;
    }

    public async Task<int> DeleteVerificationLogsAsync(DeleteCardVerificationLogsRequest request, CancellationToken cancellationToken)
    {
        return await _cardVerificationRepository.DeleteLogsAsync(request.Ids, cancellationToken);
    }

    public async Task<IReadOnlyList<SuspiciousCardRecordDto>> GetSuspiciousCardsAsync(SuspiciousCardRequest request, CancellationToken cancellationToken)
    {
        var minCount = request.MinSuccessCount <= 0 ? 3 : request.MinSuccessCount;
        var staleHours = request.StaleHours <= 0 ? 2 : request.StaleHours;
        var summaries = await _cardVerificationRepository.GetSuccessSummariesAsync(
            TimeSpan.FromHours(staleHours),
            minCount,
            cancellationToken);

        if (summaries.Count == 0)
        {
            return Array.Empty<SuspiciousCardRecordDto>();
        }

        var cardMap = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var databasePath = await _databaseManager.PrepareDatabasePathAsync(TargetSoftware).ConfigureAwait(false);
            foreach (var key in summaries.Select(summary => summary.CardKey).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                try
                {
                    var record = await Task.Run(() => SqliteBridge.GetCardByKey(databasePath, key.Trim())).ConfigureAwait(false);
                    if (record.HasValue)
                    {
                        cardMap[key] = new CardInfo
                        {
                            Prefix_Name = record.Value.PrefixName,
                            Whom = record.Value.Whom,
                            CardType = record.Value.CardType,
                            State = record.Value.State,
                            ActivateTime_ = record.Value.ActivateTime,
                            CreateData_ = record.Value.CreateData
                        };
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogDebug(innerEx, "Failed to load card {CardKey} metadata for suspicious analysis", key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load card metadata for suspicious card analysis");
        }

        var now = DateTimeOffset.UtcNow;
        var result = new List<SuspiciousCardRecordDto>();

        foreach (var summary in summaries)
        {
            cardMap.TryGetValue(summary.CardKey, out var card);
            var activatedAt = card?.ActivateTime_ ?? 0;
            if (card is not null && activatedAt > 0)
            {
                continue;
            }

            var first = DateTime.SpecifyKind(summary.FirstSuccessAt, DateTimeKind.Utc);
            var last = DateTime.SpecifyKind(summary.LastSuccessAt, DateTimeKind.Utc);
            var lastOffset = new DateTimeOffset(last);
            var duration = now - lastOffset;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            DateTimeOffset? createdAt = null;
            if (card?.CreateData_ is long created && created > 0)
            {
                try
                {
                    createdAt = DateTimeOffset.FromUnixTimeSeconds(created).ToUniversalTime();
                }
                catch
                {
                    createdAt = null;
                }
            }

            result.Add(new SuspiciousCardRecordDto
            {
                CardKey = summary.CardKey,
                Whom = card?.Whom,
                CardType = card?.CardType,
                State = card?.State,
                SuccessCount = summary.SuccessCount,
                FirstSuccessAt = new DateTimeOffset(first),
                LastSuccessAt = lastOffset,
                DurationSinceLast = duration,
                CardCreatedAt = createdAt
            });
        }

        return result
            .OrderByDescending(item => item.DurationSinceLast)
            .ThenByDescending(item => item.SuccessCount)
            .ToList();
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static DateTimeOffset? ParseTime(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (long.TryParse(trimmed, out var unixSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToUniversalTime();
            }
            catch
            {
                return null;
            }
        }

        var formats = new[]
        {
            "yyyy-MM-dd-HH:mm",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd-HH:mm",
            "yyyy-MM-dd"
        };

        if (DateTimeOffset.TryParseExact(trimmed, formats, null, System.Globalization.DateTimeStyles.AssumeLocal | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            return exact.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(trimmed, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }
}
