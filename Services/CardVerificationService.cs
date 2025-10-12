using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Services;

public sealed class CardVerificationService
{
    private const string TargetSoftware = "炫舞";
    private const int MaxUniqueDownloadLinks = 3;

    private readonly CardService _cardService;
    private readonly LanzouLinkService _lanzouLinkService;
    private readonly CardVerificationRepository _repository;
    private readonly ILogger<CardVerificationService> _logger;

    public CardVerificationService(
        CardService cardService,
        LanzouLinkService lanzouLinkService,
        CardVerificationRepository repository,
        ILogger<CardVerificationService> logger)
    {
        _cardService = cardService;
        _lanzouLinkService = lanzouLinkService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<CardVerificationResult> VerifyAsync(string cardKey, string ipAddress, CancellationToken cancellationToken)
    {
        var normalizedCardKey = cardKey.Trim();
        CardInfo? card;
        try
        {
            card = await _cardService.GetCardByKeyAsync(TargetSoftware, normalizedCardKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query card information for {CardKey}", normalizedCardKey);
            var attemptNumber = await _repository.RecordAttemptAsync(
                normalizedCardKey,
                ipAddress,
                wasSuccessful: false,
                downloadLinkId: null,
                downloadUrl: null,
                extractionCode: null,
                cancellationToken: cancellationToken);

            return new CardVerificationResult
            {
                CardKey = normalizedCardKey,
                VerificationPassed = false,
                AttemptNumber = attemptNumber,
                Message = "查询卡密信息失败，请稍后再试"
            };
        }

        var now = GetCurrentMomentStamp();
        var expiresAt = ResolveExpiration(card);
        var verificationPassed = false;
        string message;

        if (card == null)
        {
            message = "卡密不存在，请确认后重试";
        }
        else if (expiresAt.HasValue && expiresAt.Value > 0 && expiresAt.Value <= now)
        {
            message = "卡密已过期";
        }
        else if (!IsEnabled(card))
        {
            message = "卡密未启用，请联系管理员";
        }
        else
        {
            verificationPassed = true;
            message = "卡密验证成功";
        }

        LanzouLinkInfo? assignedLink = null;
        var downloadHistoryDtos = new List<DownloadLinkDto>();
        var hasReachedLinkLimit = false;

        var remainingQuota = MaxUniqueDownloadLinks;


        if (verificationPassed)
        {
            var existingHistory = await LoadDownloadHistorySafeAsync(normalizedCardKey, cancellationToken);
            var uniqueHistory = BuildUniqueHistory(existingHistory, out var usedLinkIds);
            CardDownloadHistoryEntry? newlyAssignedEntry = null;
            var assignedLinkWasNew = false;


            remainingQuota = Math.Max(MaxUniqueDownloadLinks - usedLinkIds.Count, 0);
            hasReachedLinkLimit = remainingQuota <= 0;

            hasReachedLinkLimit = usedLinkIds.Count >= MaxUniqueDownloadLinks;


            if (hasReachedLinkLimit)
            {
                message = "卡密验证成功，已达到下载链接获取上限，请使用历史链接。";
            }
            else
            {
                assignedLink = await TryAssignDownloadLinkAsync(normalizedCardKey, usedLinkIds, cancellationToken);
                if (assignedLink is null)
                {
                    message = "卡密验证成功，但暂无可用下载链接，请稍后再试。";
                }
                else
                {
                    if (usedLinkIds.Add(assignedLink.Id))
                    {
                        assignedLinkWasNew = true;
                        newlyAssignedEntry = new CardDownloadHistoryEntry(
                            assignedLink.Id,
                            assignedLink.Url,
                            assignedLink.ExtractionCode,
                            DateTime.UtcNow);
                        uniqueHistory.Add(newlyAssignedEntry);

                        remainingQuota = Math.Max(MaxUniqueDownloadLinks - usedLinkIds.Count, 0);

                    }
                    else if (uniqueHistory.Count > 0)
                    {
                        message = "卡密验证成功，暂无新的下载链接，已为您保留历史链接。";
                    }
                }


                hasReachedLinkLimit = remainingQuota <= 0;

                hasReachedLinkLimit = usedLinkIds.Count >= MaxUniqueDownloadLinks;

                if (assignedLinkWasNew && hasReachedLinkLimit)
                {
                    message = "卡密验证成功，您已达到下载链接获取上限，请妥善保存历史链接。";
                }

                else if (!hasReachedLinkLimit && assignedLinkWasNew && remainingQuota > 0)
                {
                    message = $"卡密验证成功，已为您分配新的下载链接（还可领取 {remainingQuota} 条）。";
                }

            }

            downloadHistoryDtos = uniqueHistory
                .OrderBy(entry => entry.CreatedAt)
                .Select(entry => ToDownloadLinkDto(entry, newlyAssignedEntry is not null && ReferenceEquals(entry, newlyAssignedEntry)))
                .ToList();
        }

        var attemptNumberFinal = await RecordAttemptSafeAsync(
            normalizedCardKey,
            ipAddress,
            verificationPassed,
            assignedLink,
            cancellationToken);

        var primaryDownload = downloadHistoryDtos.Count > 0 ? downloadHistoryDtos[^1] : null;

        return new CardVerificationResult
        {
            CardKey = normalizedCardKey,
            VerificationPassed = verificationPassed,
            AttemptNumber = attemptNumberFinal,
            ExpiresAt = expiresAt,
            Download = primaryDownload,
            DownloadHistory = downloadHistoryDtos,
            HasReachedLinkLimit = hasReachedLinkLimit,

            RemainingLinkQuota = remainingQuota,

            Message = message
        };
    }

    private async Task<LanzouLinkInfo?> TryAssignDownloadLinkAsync(string cardKey, ISet<long> usedLinkIds, CancellationToken cancellationToken)
    {
        var links = await _lanzouLinkService.GetAvailableLinksAsync(cancellationToken);
        if (links.Count == 0)
        {
            _logger.LogWarning("No Lanzou download links available to assign");
            return null;
        }

        IDictionary<long, int> usageCounts;
        long? lastLinkId;

        try
        {
            usageCounts = await _repository.GetSuccessfulAssignmentsByLinkAsync(cancellationToken);
            lastLinkId = await _repository.GetLastSuccessfulLinkForCardAsync(cardKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load previous link assignments");
            usageCounts = new Dictionary<long, int>();
            lastLinkId = null;
        }

        var ranked = links
            .Select(link => (Link: link, Usage: usageCounts.TryGetValue(link.Id, out var count) ? count : 0))
            .OrderBy(item => item.Usage)
            .ThenByDescending(item => item.Link.CreatedAt)
            .ThenByDescending(item => item.Link.Id)
            .ToList();

        if (ranked.Count == 0)
        {
            return null;
        }

        var latestCreatedAt = links.Max(link => link.CreatedAt);
        var windowStart = latestCreatedAt.AddHours(-24);
        var windowCandidates = ranked
            .Where(item => item.Link.CreatedAt >= windowStart)
            .ToList();

        var preferred = PickRandomCandidate(windowCandidates, rankedItem =>
            !usedLinkIds.Contains(rankedItem.Link.Id) && rankedItem.Link.Id != lastLinkId);

        preferred ??= PickRandomCandidate(windowCandidates, rankedItem =>
            !usedLinkIds.Contains(rankedItem.Link.Id));

        if (preferred is null && lastLinkId.HasValue)
        {
            preferred = PickRandomCandidate(windowCandidates, rankedItem => rankedItem.Link.Id != lastLinkId.Value);
        }

        preferred ??= PickRandomCandidate(windowCandidates, _ => true);

        preferred ??= PickRandomCandidate(ranked, rankedItem =>
            !usedLinkIds.Contains(rankedItem.Link.Id) && rankedItem.Link.Id != lastLinkId);

        preferred ??= PickRandomCandidate(ranked, rankedItem => !usedLinkIds.Contains(rankedItem.Link.Id));

        if (preferred is null && lastLinkId.HasValue)
        {
            preferred = PickRandomCandidate(ranked, rankedItem => rankedItem.Link.Id != lastLinkId.Value);
        }

        return (preferred ?? ranked.First()).Link;
    }

    private static (LanzouLinkInfo Link, int Usage)? PickRandomCandidate(
        IReadOnlyList<(LanzouLinkInfo Link, int Usage)> candidates,
        Func<(LanzouLinkInfo Link, int Usage), bool> predicate)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var filtered = candidates.Where(predicate).ToList();
        if (filtered.Count == 0)
        {
            return null;
        }

        var minUsage = filtered.Min(item => item.Usage);
        var finalists = filtered.Where(item => item.Usage == minUsage).ToList();
        if (finalists.Count == 0)
        {
            return null;
        }

        var selectedIndex = RandomNumberGenerator.GetInt32(finalists.Count);
        return finalists[selectedIndex];
    }

    private async Task<IReadOnlyList<CardDownloadHistoryEntry>> LoadDownloadHistorySafeAsync(string cardKey, CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.GetDownloadHistoryForCardAsync(cardKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load download history for {CardKey}", cardKey);
            return Array.Empty<CardDownloadHistoryEntry>();
        }
    }

    private static List<CardDownloadHistoryEntry> BuildUniqueHistory(IReadOnlyList<CardDownloadHistoryEntry> entries, out HashSet<long> usedLinkIds)
    {
        usedLinkIds = new HashSet<long>();
        var unique = new List<CardDownloadHistoryEntry>();

        if (entries == null || entries.Count == 0)
        {
            return unique;
        }

        foreach (var entry in entries.OrderBy(e => e.CreatedAt))
        {
            if (entry.DownloadLinkId <= 0)
            {
                continue;
            }

            if (usedLinkIds.Add(entry.DownloadLinkId))
            {
                unique.Add(entry);
            }
        }

        return unique;
    }

    private static DownloadLinkDto ToDownloadLinkDto(CardDownloadHistoryEntry entry, bool isNew)
    {
        return new DownloadLinkDto
        {
            LinkId = entry.DownloadLinkId,
            Url = entry.DownloadUrl ?? string.Empty,
            ExtractionCode = entry.ExtractionCode ?? string.Empty,
            AssignedAt = FormatAssignedAt(entry.CreatedAt),
            IsNew = isNew
        };
    }

    private static long FormatAssignedAt(DateTime createdAt)
    {
        DateTime localTime;
        if (createdAt.Kind == DateTimeKind.Unspecified)
        {
            localTime = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc).ToLocalTime();
        }
        else if (createdAt.Kind == DateTimeKind.Utc)
        {
            localTime = createdAt.ToLocalTime();
        }
        else
        {
            localTime = createdAt;
        }

        var formatted = localTime.ToString("yyyyMMddHHmmss");
        return long.TryParse(formatted, out var value) ? value : 0;
    }

    private static long GetCurrentMomentStamp()
    {
        var formatted = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        return long.TryParse(formatted, out var value) ? value : 0;
    }

    private static long? ResolveExpiration(CardInfo? card)
    {
        if (card == null)
        {
            return null;
        }

        // Upstream services emit placeholder expirations around 1970-01-02/03 to mean "no expiry".
        const long PlaceholderCutoff = 19700103000000;

        var candidates = new List<long>();
        if (card.ExpiredTime__ > 0)
        {
            candidates.Add(card.ExpiredTime__);
        }

        if (card.ExpiredTime_ > 0)
        {
            candidates.Add(card.ExpiredTime_);
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var normalized = candidates
            .Select(NormalizeExpiration)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (normalized.Count == 0)
        {
            return 0;
        }

        var resolved = normalized.Max();
        if (resolved <= 0 || resolved < PlaceholderCutoff)
        {
            return 0;
        }

        return resolved;
    }

    private static bool IsEnabled(CardInfo card)
    {
        if (string.IsNullOrWhiteSpace(card.State))
        {
            return false;
        }

        return string.Equals(card.State, "启用", StringComparison.OrdinalIgnoreCase);
    }

    private static long? NormalizeExpiration(long raw)
    {
        // Values returned by the upstream service might be in seconds, milliseconds, or already formatted.
        if (raw < 0)
        {
            return raw;
        }

        if (raw < 100_000_000_000) // assume seconds since Unix epoch
        {
            return TryFormatFromUnixTime(raw, fromMilliseconds: false);
        }

        if (raw < 100_000_000_000_000) // assume milliseconds since Unix epoch
        {
            return TryFormatFromUnixTime(raw, fromMilliseconds: true);
        }

        // Already formatted as yyyyMMddHHmmss or similar scale.
        return raw;
    }

    private async Task<long> RecordAttemptSafeAsync(
        string cardKey,
        string ipAddress,
        bool verificationPassed,
        LanzouLinkInfo? assignedLink,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.RecordAttemptAsync(
                cardKey,
                ipAddress,
                verificationPassed,
                assignedLink?.Id,
                assignedLink?.Url,
                assignedLink?.ExtractionCode,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record verification attempt for {CardKey}", cardKey);
            return 1;
        }
    }

    private static long TryFormatFromUnixTime(long raw, bool fromMilliseconds)
    {
        try
        {
            var instant = fromMilliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
                : DateTimeOffset.FromUnixTimeSeconds(raw);

            var formatted = instant.ToLocalTime().ToString("yyyyMMddHHmmss");
            return long.TryParse(formatted, out var value) ? value : raw;
        }
        catch (ArgumentOutOfRangeException)
        {
            return raw;
        }
    }
}

