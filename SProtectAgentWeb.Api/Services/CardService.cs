using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Services
{
    public class CardService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly ILogger<CardService> _logger;
        private readonly PermissionHelper _permissionHelper;
        private readonly UsageDistributionCacheRepository _usageCacheRepository;
        private readonly IpLocationCacheRepository _ipLocationCacheRepository;
        private readonly SettlementRateService _settlementRateService;

        private static readonly char[] CardKeyAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private const int RandomCardSuffixLength = 32;
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private sealed class CachedIpLocation
        {
            public string Province { get; init; } = string.Empty;
            public string City { get; init; } = string.Empty;
            public string District { get; init; } = string.Empty;
            public long UpdatedAt { get; init; }

            public (string Province, string City, string District) AsTuple()
            {
                return (Province, City, District);
            }
        }

        private static readonly ConcurrentDictionary<string, CachedIpLocation> IpLocationCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> BackgroundIpRefreshSet = new(StringComparer.OrdinalIgnoreCase);
        private const long IpLocationCacheTtlSeconds = 24 * 60 * 60;
        private const int MaxImmediateIpResolutions = 50;
        private static readonly Func<string, Task<(string Province, string City, string District)>>[] IpResolvers =
        {
            QueryLocationFromPcOnlineAsync,
            QueryLocationFromIpApiAsync
        };
        private static readonly SemaphoreSlim IpResolverSemaphore = new(5);
        private static readonly (string Province, string City, string District) EmptyLocation = (string.Empty, string.Empty, string.Empty);
        private static readonly Uri PcOnlineReferer = new("https://whois.pconline.com.cn/");

        public CardService(
            DatabaseManager databaseManager,
            ILogger<CardService> logger,
            PermissionHelper permissionHelper,
            UsageDistributionCacheRepository usageCacheRepository,
            IpLocationCacheRepository ipLocationCacheRepository,
            SettlementRateService settlementRateService)
        {
            _databaseManager = databaseManager;
            _logger = logger;
            _permissionHelper = permissionHelper;
            _usageCacheRepository = usageCacheRepository;
            _ipLocationCacheRepository = ipLocationCacheRepository;
            _settlementRateService = settlementRateService;
        }

        static CardService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<(IList<CardInfo> Items, long Total)> GetCardListAsync(CardQueryParams query)
        {
            IReadOnlyList<string> creators = await ResolveCreatorUsernamesAsync(
                query.Software,
                query.WhomList,
                query.CurrentAgent,
                query.IncludeDescendants).ConfigureAwait(false);

            var dbPath = await _databaseManager
                .PrepareDatabasePathAsync(query.Software)
                .ConfigureAwait(false);

            var keywordList = query.Keywords?.ToArray() ?? Array.Empty<string>();

            var options = new SqliteBridge.CardListQueryOptions(
                query.Page,
                query.PageSize,
                query.Status ?? string.Empty,
                creators,
                keywordList,
                query.SearchType);

            var result = await Task.Run(() => SqliteBridge.QueryCardList(dbPath, options)).ConfigureAwait(false);

            var items = result.Cards.Select(MapCardRecord).ToList();

            if (items.Count > 0 && result.Bindings.Count > 0)
            {
                var grouped = result.Bindings
                    .Where(binding => !string.IsNullOrWhiteSpace(binding.Card))
                    .GroupBy(binding => binding.Card!, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(binding => binding.PcSign)
                            .Where(code => !string.IsNullOrWhiteSpace(code))
                            .Select(code => code!.Trim())
                            .Where(code => code.Length > 0)
                            .Distinct(StringComparer.Ordinal)
                            .ToList(),
                        StringComparer.Ordinal);

                foreach (var item in items)
                {
                    if (grouped.TryGetValue(item.Prefix_Name, out var codes))
                    {
                        item.MachineCodes = codes;
                    }
                }
            }

            return (items, result.Total);
        }

        public async Task<CardInfo?> GetCardByKeyAsync(string software, string cardKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cardKey))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var normalized = cardKey.Trim();
            if (normalized.Length == 0)
            {
                return null;
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            var record = await Task.Run(() => SqliteBridge.GetCardByKey(dbPath, normalized)).ConfigureAwait(false);
            return record.HasValue ? MapCardRecord(record.Value) : null;
        }

        private async Task<IReadOnlyList<string>> ResolveCreatorUsernamesAsync(
            string software,
            IEnumerable<string>? rawCreators,
            string? currentAgent,
            bool includeDescendants)
        {
            var baseCreators = (rawCreators ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (baseCreators.Count == 0 && !string.IsNullOrWhiteSpace(currentAgent))
            {
                baseCreators.Add(currentAgent.Trim());
            }

            if (baseCreators.Count == 0)
            {
                return new List<string>();
            }

            if (!includeDescendants)
            {
                return baseCreators;
            }

            var agentInfos = await LoadAgentHierarchyAsync(software).ConfigureAwait(false);

            var resolved = new HashSet<string>(baseCreators, StringComparer.OrdinalIgnoreCase);

            foreach (var creator in baseCreators)
            {
                foreach (var agent in agentInfos)
                {
                    if (string.IsNullOrWhiteSpace(agent.User))
                    {
                        continue;
                    }

                    if (_permissionHelper.IsChildAgent(agent.FNode, creator))
                    {
                        resolved.Add(agent.User.Trim());
                    }
                }
            }

            return resolved
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .ToList();
        }

        private async Task<IList<AgentHierarchyInfo>> LoadAgentHierarchyAsync(string software)
        {
            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            var records = await Task.Run(() => SqliteBridge.GetAgents(dbPath)).ConfigureAwait(false);
            return records
                .Select(record => new AgentHierarchyInfo { User = record.User, FNode = record.FNode })
                .ToList();
        }

        public async Task<int> UnbindCardAsync(string software, string cardKey)
        {
            if (string.IsNullOrWhiteSpace(cardKey))
            {
                return 0;
            }

            var normalized = cardKey.Trim();
            if (normalized.Length == 0)
            {
                return 0;
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            var affected = await Task.Run(() => SqliteBridge.DeleteCardBindings(dbPath, normalized)).ConfigureAwait(false);
            return affected;
        }

        public async Task EnableCardAsync(string software, string cardKey)
        {
            var normalized = cardKey?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return;
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            await Task.Run(() => SqliteBridge.UpdateCardState(dbPath, normalized, "启用", resetBanTime: true, resetGiveBackBanTime: false))
                .ConfigureAwait(false);
        }

        public async Task DisableCardAsync(string software, string cardKey)
        {
            var normalized = cardKey?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return;
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            await Task.Run(() => SqliteBridge.UpdateCardState(dbPath, normalized, "禁用", resetBanTime: false, resetGiveBackBanTime: false))
                .ConfigureAwait(false);
        }

        public async Task EnableCardWithBanTimeReturnAsync(string software, string cardKey)
        {
            var normalized = cardKey?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                return;
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            await Task.Run(() => SqliteBridge.UpdateCardState(dbPath, normalized, "启用", resetBanTime: true, resetGiveBackBanTime: true))
                .ConfigureAwait(false);
        }

        public async Task<GenerateCardsResponse> GenerateCardsAsync(string software, string creator, GenerateCardsRequest request)
        {
            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            var cardTypeRecord = await Task.Run(() => SqliteBridge.GetCardTypeByName(dbPath, request.CardType)).ConfigureAwait(false);

            if (!cardTypeRecord.HasValue)
            {
                throw new InvalidOperationException("卡密类型不存在");
            }

            var cardType = MapCardType(cardTypeRecord.Value);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sample = new List<string>();
            var generated = new List<string>();
            var generationId = Guid.NewGuid().ToString("N");

            var prefix = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.CustomPrefix))
            {
                prefix = request.CustomPrefix!.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(cardType.Prefix))
            {
                prefix = cardType.Prefix!.Trim();
            }

            // 允许不同前缀长度，最终卡密总长度 = 前缀长度 + 32 位随机码。
            // 仍保留基本防御，避免异常长的前缀导致生成超长卡密。
            if (prefix.Length > RandomCardSuffixLength)
            {
                throw new InvalidOperationException($"卡密前缀长度不能超过{RandomCardSuffixLength}位");
            }

            using var rng = RandomNumberGenerator.Create();

            var activateTime = cardType.ActivateTime_ > 0 ? cardType.ActivateTime_ : 0;
            var lastLoginTime = cardType.LastLoginTime_ > 0 ? cardType.LastLoginTime_ : 0;
            var delState = cardType.Delstate;
            var cardPrice = cardType.Price;
            var bindFlag = cardType.Bind;
            var openNum = cardType.OpenNum;
            var bindIp = cardType.BindIP;
            var bindMachineNum = cardType.BindMachineNum > 0 ? cardType.BindMachineNum : 1;
            var lockBindPcSign = cardType.LockBindPcsign;
            var attrUnbindLimitTime = cardType.Attr_UnBindLimitTime;
            var attrUnbindDeductTime = cardType.Attr_UnBindDeductTime;
            var attrUnbindFreeCount = cardType.Attr_UnBindFreeCount;
            var attrUnbindMaxCount = cardType.Attr_UnBindMaxCount;
            const int cty = 1;
            const int expiredTimeSecondary = 0;

            var expiredTimePrimary = cardType.ExpiredTime_ > 0 ? cardType.ExpiredTime_ : 0;
            if (expiredTimePrimary <= 0 && cardType.Duration > 0)
            {
                expiredTimePrimary = cardType.Duration;
            }

            var insertRecords = new List<SqliteBridge.CardInsertRecord>(request.Quantity);

            try
            {
                for (var i = 0; i < request.Quantity; i++)
                {
                    var cardKey = BuildCardKey(prefix, rng);
                    generated.Add(cardKey);
                    if (sample.Count < 5)
                    {
                        sample.Add(cardKey);
                    }

                    insertRecords.Add(new SqliteBridge.CardInsertRecord(
                        cardKey,
                        creator,
                        request.CardType,
                        cardType.FYI,
                        "启用",
                        bindFlag,
                        openNum,
                        string.Empty,
                        request.Remarks,
                        now,
                        activateTime,
                        expiredTimePrimary,
                        lastLoginTime,
                        delState,
                        cardPrice,
                        cty,
                        expiredTimeSecondary,
                        attrUnbindLimitTime,
                        attrUnbindDeductTime,
                        attrUnbindFreeCount,
                        attrUnbindMaxCount,
                        bindIp,
                        bindMachineNum,
                        lockBindPcSign));
                }

                await Task.Run(() => SqliteBridge.InsertCards(dbPath, insertRecords)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate cards for {Software}", software);
                throw;
            }

            var totalCost = cardType.Price * request.Quantity;
            return new GenerateCardsResponse
            {
                GeneratedCount = request.Quantity,
                CardType = request.CardType,
                SampleCards = sample,
                GeneratedCards = generated,
                GenerationId = generationId,
                Cost = new Dictionary<string, object>
                {
                    ["balance_deducted"] = totalCost,
                    ["time_deducted"] = 0,
                },
            };
        }

        public async Task<ActivatedCardCountResponse> CountActivatedCardsAsync(
            ActivatedCardCountQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var creators = await ResolveCreatorUsernamesAsync(
                query.Software,
                query.WhomList,
                query.CurrentAgent,
                query.IncludeDescendants).ConfigureAwait(false);

            var dbPath = await _databaseManager
                .PrepareDatabasePathAsync(query.Software)
                .ConfigureAwait(false);

            var cardTypes = query.CardTypes?.ToArray() ?? Array.Empty<string>();

            var options = new SqliteBridge.ActivatedCardQueryOptions(
                query.Status ?? string.Empty,
                query.StartTime,
                query.EndTime,
                creators,
                cardTypes);

            var result = await Task.Run(() => SqliteBridge.QueryActivatedCards(dbPath, options), cancellationToken)
                .ConfigureAwait(false);

            var details = result.Records
                .Select(record => new ActivatedCardDetail
                {
                    Card = record.Card,
                    ActivateTime = record.ActivateTime,
                    ActivateTimeText = FormatActivateTime(record.ActivateTime)
                })
                .ToList();

            var settlementResult = await BuildSettlementSummaryAsync(
                    query,
                    creators,
                    dbPath,
                    result.Total,
                    cancellationToken)
                .ConfigureAwait(false);
            var summaries = settlementResult.Items?.ToList() ?? new List<ActivatedCardSettlementSummary>();
            var totalAmount = settlementResult.TotalAmount;

            return new ActivatedCardCountResponse
            {
                Count = result.Total,
                Cards = details,
                Settlements = summaries,
                TotalAmount = totalAmount,
            };
        }

        private async Task<(IReadOnlyList<ActivatedCardSettlementSummary> Items, decimal TotalAmount)> BuildSettlementSummaryAsync(
            ActivatedCardCountQuery query,
            IReadOnlyList<string> creators,
            string databasePath,
            long initialTotal,
            CancellationToken cancellationToken)
        {
            try
            {
                var rateAgent = ResolveSettlementAgentUsername(query);
                var fallbackAgent = query.CurrentAgent?.Trim();
                if (!string.IsNullOrWhiteSpace(fallbackAgent) &&
                    string.Equals(fallbackAgent, rateAgent, StringComparison.OrdinalIgnoreCase))
                {
                    fallbackAgent = null;
                }

                var rateDictionary = await _settlementRateService
                    .GetRateDictionaryAsync(query.Software, rateAgent, fallbackAgent, cancellationToken)
                    .ConfigureAwait(false);

                var candidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in rateDictionary.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        candidates.Add(key.Trim());
                    }
                }

                if (query.CardTypes is { Count: > 0 })
                {
                    foreach (var type in query.CardTypes)
                    {
                        if (!string.IsNullOrWhiteSpace(type))
                        {
                            candidates.Add(type.Trim());
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    var records = await Task.Run(() => SqliteBridge.GetCardTypes(databasePath), cancellationToken)
                        .ConfigureAwait(false);
                    foreach (var record in records)
                    {
                        if (!string.IsNullOrWhiteSpace(record.Name))
                        {
                            candidates.Add(record.Name.Trim());
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    return (Array.Empty<ActivatedCardSettlementSummary>(), 0m);
                }

                var summaries = new List<ActivatedCardSettlementSummary>();
                decimal totalAmount = 0m;
                var status = query.Status ?? string.Empty;
                var singleFilter = query.CardTypes is { Count: 1 } ? query.CardTypes[0] : null;

                foreach (var cardType in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(cardType))
                    {
                        continue;
                    }

                    long count;
                    if (!string.IsNullOrWhiteSpace(singleFilter) && string.Equals(singleFilter, cardType, StringComparison.Ordinal))
                    {
                        count = initialTotal;
                    }
                    else
                    {
                        var queryOptions = new SqliteBridge.ActivatedCardQueryOptions(
                            status,
                            query.StartTime,
                            query.EndTime,
                            creators,
                            new[] { cardType });

                        var typeResult = await Task.Run(() => SqliteBridge.QueryActivatedCards(databasePath, queryOptions), cancellationToken)
                            .ConfigureAwait(false);
                        count = typeResult.Total;
                    }

                    if (count <= 0)
                    {
                        continue;
                    }

                    rateDictionary.TryGetValue(cardType, out var price);
                    var total = price * count;
                    summaries.Add(new ActivatedCardSettlementSummary
                    {
                        CardType = cardType,
                        Count = count,
                        Price = price,
                        Total = total
                    });

                    totalAmount += total;
                }

                return (summaries, totalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build settlement summary for {Software}", query.Software);
                return (Array.Empty<ActivatedCardSettlementSummary>(), 0m);
            }
        }

        private static string ResolveSettlementAgentUsername(ActivatedCardCountQuery query)
        {
            if (query == null)
            {
                return string.Empty;
            }

            var currentAgent = query.CurrentAgent?.Trim() ?? string.Empty;

            if (query.WhomList is { Count: > 0 })
            {
                var normalized = query.WhomList
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (normalized.Count == 1)
                {
                    return normalized[0]!;
                }
            }

            return currentAgent;
        }


        public async Task<RecentActivationTrendResponse> GetRecentActivationTrendAsync(
            string software,
            string currentAgent,
            bool includeDescendants,
            bool onlyDescendants)
        {
            var dbPath = await _databaseManager
                .PrepareDatabasePathAsync(software)
                .ConfigureAwait(false);

            var normalizedCurrent = currentAgent?.Trim() ?? string.Empty;

            var now = DateTimeOffset.Now;
            var startDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(-6);
            var startTimestamp = startDay.ToUnixTimeSeconds();
            var endTimestamp = startDay.AddDays(7).ToUnixTimeSeconds() - 1;

            var categories = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                categories.Add(startDay.AddDays(i).ToString("yyyy-MM-dd"));
            }

            if (onlyDescendants)
            {
                if (string.IsNullOrWhiteSpace(normalizedCurrent))
                {
                    return BuildEmptyTrend();
                }

                var agentInfos = await LoadAgentHierarchyAsync(software).ConfigureAwait(false);

                var agentMap = new Dictionary<string, AgentHierarchyInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var info in agentInfos)
                {
                    var username = info.User?.Trim();
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        continue;
                    }

                    info.User = username;
                    if (!agentMap.ContainsKey(username))
                    {
                        agentMap.Add(username, info);
                    }
                }

                var directChildren = agentMap.Values
                    .Where(info => _permissionHelper.IsDirectChildAgent(info.FNode, normalizedCurrent))
                    .Select(info => info.User)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (directChildren.Count == 0)
                {
                    return BuildEmptyTrend();
                }

                var descendantToChild = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in agentMap)
                {
                    var username = pair.Key;
                    if (username.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!_permissionHelper.IsChildAgent(pair.Value.FNode, normalizedCurrent))
                    {
                        continue;
                    }

                    var directChild = ResolveDirectChild(username);
                    if (string.IsNullOrWhiteSpace(directChild))
                    {
                        continue;
                    }

                    if (!descendantToChild.ContainsKey(username))
                    {
                        descendantToChild.Add(username, directChild);
                    }
                }

                if (descendantToChild.Count == 0)
                {
                    return BuildEmptyTrendWithSeries(directChildren);
                }

                var normalizedCreators = descendantToChild.Keys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (normalizedCreators.Count == 0)
                {
                    return BuildEmptyTrendWithSeries(directChildren);
                }

                var trendRows = await Task.Run(() => SqliteBridge.QueryActivationTrend(
                        dbPath,
                        startTimestamp,
                        endTimestamp,
                        normalizedCreators,
                        groupByWhom: true))
                    .ConfigureAwait(false);

                var aggregate = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
                foreach (var child in directChildren)
                {
                    var dayMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    foreach (var day in categories)
                    {
                        dayMap[day] = 0;
                    }
                    aggregate[child] = dayMap;
                }

                var dailyTotals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var day in categories)
                {
                    dailyTotals[day] = 0;
                }

                foreach (var row in trendRows)
                {
                    var creator = row.Whom?.Trim();
                    if (string.IsNullOrWhiteSpace(creator))
                    {
                        continue;
                    }

                    if (!descendantToChild.TryGetValue(creator, out var child))
                    {
                        continue;
                    }

                    if (!aggregate.TryGetValue(child, out var childMap))
                    {
                        var newMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                        foreach (var day in categories)
                        {
                            newMap[day] = 0;
                        }
                        childMap = newMap;
                        aggregate[child] = childMap;
                    }

                    var dayKey = row.Day?.Trim();
                    if (string.IsNullOrWhiteSpace(dayKey))
                    {
                        continue;
                    }

                    if (childMap.ContainsKey(dayKey))
                    {
                        childMap[dayKey] += row.Count;
                    }

                    if (dailyTotals.ContainsKey(dayKey))
                    {
                        dailyTotals[dayKey] += row.Count;
                    }
                }

                var series = new List<DailyActivationSeries>();
                foreach (var child in directChildren)
                {
                    aggregate.TryGetValue(child, out var childMap);
                    childMap ??= new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                    var points = new List<DailyActivationPoint>();
                    foreach (var day in categories)
                    {
                        childMap.TryGetValue(day, out var count);
                        points.Add(new DailyActivationPoint { Date = day, Count = count });
                    }

                    var total = points.Sum(point => point.Count);
                    series.Add(new DailyActivationSeries
                    {
                        Agent = child,
                        DisplayName = child,
                        Points = points,
                        Total = total
                    });
                }

                var orderedSeries = series
                    .OrderByDescending(item => item.Total)
                    .ThenBy(item => item.Agent, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var totalPointList = new List<DailyActivationPoint>();
                foreach (var day in categories)
                {
                    dailyTotals.TryGetValue(day, out var count);
                    totalPointList.Add(new DailyActivationPoint { Date = day, Count = count });
                }

                return new RecentActivationTrendResponse
                {
                    Categories = categories,
                    Points = totalPointList,
                    Series = orderedSeries
                };

                string? ResolveDirectChild(string username)
                {
                    var current = username;
                    while (agentMap.TryGetValue(current, out var info))
                    {
                        var parent = _permissionHelper.GetAgentParent(info.FNode);
                        if (string.IsNullOrWhiteSpace(parent))
                        {
                            return null;
                        }

                        if (parent.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                        {
                            return current;
                        }

                        current = parent.Trim();
                    }

                    return null;
                }
            }

            var baseCreators = string.IsNullOrWhiteSpace(normalizedCurrent)
                ? new List<string>()
                : new List<string> { normalizedCurrent };

            var creators = await ResolveCreatorUsernamesAsync(
                software,
                baseCreators,
                normalizedCurrent,
                includeDescendants);

            if (creators.Count == 0)
            {
                return BuildEmptyTrend();
            }

            var creatorArray = creators
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var trendRowsBasic = await Task.Run(() => SqliteBridge.QueryActivationTrend(
                    dbPath,
                    startTimestamp,
                    endTimestamp,
                    creatorArray,
                    groupByWhom: false))
                .ConfigureAwait(false);

            var map = trendRowsBasic
                .Where(row => !string.IsNullOrWhiteSpace(row.Day))
                .GroupBy(row => row.Day!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count), StringComparer.Ordinal);

            var totalPoints = new List<DailyActivationPoint>();
            foreach (var day in categories)
            {
                map.TryGetValue(day, out var count);
                totalPoints.Add(new DailyActivationPoint { Date = day, Count = count });
            }

            return new RecentActivationTrendResponse
            {
                Categories = categories,
                Points = totalPoints
            };

            RecentActivationTrendResponse BuildEmptyTrend()
            {
                var emptyPoints = categories
                    .Select(day => new DailyActivationPoint { Date = day, Count = 0 })
                    .ToList();

                return new RecentActivationTrendResponse
                {
                    Categories = categories,
                    Points = emptyPoints
                };
            }

            RecentActivationTrendResponse BuildEmptyTrendWithSeries(IEnumerable<string> children)
            {
                var baseResponse = BuildEmptyTrend();
                var series = children
                    .Select(child => new DailyActivationSeries
                    {
                        Agent = child,
                        DisplayName = child,
                        Points = categories.Select(day => new DailyActivationPoint { Date = day, Count = 0 }).ToList(),
                        Total = 0
                    })
                    .ToList();

                baseResponse.Series = series;
                return baseResponse;
            }
        }

        public async Task<UsageDistributionResponse> GetUsageDistributionAsync(string software, string currentAgent, bool includeDescendants)
        {
            var databasePath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);

            var baseCreators = string.IsNullOrWhiteSpace(currentAgent)
                ? Array.Empty<string>()
                : new[] { currentAgent };

            var creators = await ResolveCreatorUsernamesAsync(
                software,
                baseCreators,
                currentAgent,
                includeDescendants);

            var normalizedCreators = creators
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedCreators.Count == 0)
            {
                var cached = await _usageCacheRepository
                    .GetEntriesAsync(software, new[] { UsageDistributionCacheRepository.AllKey }, CancellationToken.None)
                    .ConfigureAwait(false);

                var cachedResponse = cached.FirstOrDefault()?.Response;
                if (cachedResponse != null)
                {
                    return cachedResponse;
                }

                return await ComputeUsageDistributionAsync(databasePath, null, CancellationToken.None, software).ConfigureAwait(false);
            }

            var cachedEntries = await _usageCacheRepository
                .GetEntriesAsync(software, normalizedCreators, CancellationToken.None)
                .ConfigureAwait(false);

            var responses = cachedEntries
                .Where(entry => entry.Response != null)
                .Select(entry => entry.Response!)
                .ToList();

            var missingCreators = normalizedCreators
                .Except(cachedEntries.Select(entry => entry.Whom), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingCreators.Count > 0)
            {
                var fallback = await ComputeUsageDistributionAsync(databasePath, missingCreators, CancellationToken.None, software).ConfigureAwait(false);
                responses.Add(fallback);
            }

            if (responses.Count == 0)
            {
                return new UsageDistributionResponse();
            }

            return MergeUsageDistributions(responses);
        }

        internal async Task<UsageDistributionResponse> ComputeUsageDistributionAsync(
            string databasePath,
            IEnumerable<string>? creators,
            CancellationToken cancellationToken,
            string? software = null,
            bool allowBackgroundRefresh = true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var creatorList = (creators ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            IReadOnlyList<string> rows;
            try
            {
                rows = await Task.Run(() => SqliteBridge.GetCardIpAddresses(databasePath, creatorList))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取卡密地区信息失败: {DatabasePath}", databasePath);
                return new UsageDistributionResponse();
            }

            var resolvedLocations = new (string Province, string City, string District)[rows.Count];
            var ipIndexMap = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < rows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var raw = rows[index];
                var parsed = ParseChinaLocation(raw);
                if (HasLocation(parsed))
                {
                    resolvedLocations[index] = parsed;
                    continue;
                }

                var ip = ExtractIpAddress(raw);
                if (string.IsNullOrEmpty(ip))
                {
                    continue;
                }

                if (!ipIndexMap.TryGetValue(ip, out var indexes))
                {
                    indexes = new List<int>();
                    ipIndexMap[ip] = indexes;
                }

                indexes.Add(index);
            }

            if (ipIndexMap.Count > 0)
            {
                var ipLocations = await ResolveIpLocationsInternalAsync(
                    ipIndexMap.Keys,
                    software,
                    MaxImmediateIpResolutions,
                    scheduleBackground: allowBackgroundRefresh && !string.IsNullOrWhiteSpace(software),
                    cancellationToken).ConfigureAwait(false);

                foreach (var pair in ipIndexMap)
                {
                    if (!ipLocations.TryGetValue(pair.Key, out var location))
                    {
                        continue;
                    }

                    foreach (var rowIndex in pair.Value)
                    {
                        resolvedLocations[rowIndex] = location;
                    }
                }
            }

            var provinceStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var cityStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var districtStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            long resolvedCount = 0;

            foreach (var location in resolvedLocations)
            {
                if (!HasLocation(location))
                {
                    continue;
                }

                resolvedCount++;

                if (!string.IsNullOrWhiteSpace(location.Province))
                {
                    provinceStats.TryGetValue(location.Province, out var count);
                    provinceStats[location.Province] = count + 1;
                }

                if (!string.IsNullOrWhiteSpace(location.City))
                {
                    var cityKey = string.Join("|", new[] { location.Province, location.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
                    cityStats.TryGetValue(cityKey, out var count);
                    cityStats[cityKey] = count + 1;
                }

                if (!string.IsNullOrWhiteSpace(location.District))
                {
                    var districtKey = string.Join("|", new[] { location.Province, location.City, location.District }.Where(part => !string.IsNullOrWhiteSpace(part)));
                    districtStats.TryGetValue(districtKey, out var count);
                    districtStats[districtKey] = count + 1;
                }
            }

            return new UsageDistributionResponse
            {
                Provinces = provinceStats
                    .Select(pair => new LocationStat { Province = pair.Key, Count = pair.Value })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                Cities = cityStats
                    .Select(pair =>
                    {
                        var segments = pair.Key.Split('|');
                        return new LocationStat
                        {
                            Province = segments.Length > 0 ? segments[0] : string.Empty,
                            City = segments.Length > 1 ? segments[1] : string.Empty,
                            Count = pair.Value
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                Districts = districtStats
                    .Select(pair =>
                    {
                        var segments = pair.Key.Split('|');
                        return new LocationStat
                        {
                            Province = segments.Length > 0 ? segments[0] : string.Empty,
                            City = segments.Length > 1 ? segments[1] : string.Empty,
                            District = segments.Length > 2 ? segments[2] : string.Empty,
                            Count = pair.Value
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                ResolvedTotal = resolvedCount,
            };
        }

        private static UsageDistributionResponse MergeUsageDistributions(IEnumerable<UsageDistributionResponse> responses)
        {
            var provinceStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var cityStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var districtStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            long total = 0;

            foreach (var response in responses ?? Array.Empty<UsageDistributionResponse>())
            {
                if (response is null)
                {
                    continue;
                }

                total += Math.Max(0, response.ResolvedTotal);

                if (response.Provinces != null)
                {
                    foreach (var item in response.Provinces)
                    {
                        if (item is null || string.IsNullOrWhiteSpace(item.Province))
                        {
                            continue;
                        }

                        provinceStats.TryGetValue(item.Province, out var count);
                        provinceStats[item.Province] = count + item.Count;
                    }
                }

                if (response.Cities != null)
                {
                    foreach (var item in response.Cities)
                    {
                        if (item is null)
                        {
                            continue;
                        }

                        var key = string.Join("|", new[] { item.Province, item.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
                        if (string.IsNullOrEmpty(key))
                        {
                            continue;
                        }

                        cityStats.TryGetValue(key, out var count);
                        cityStats[key] = count + item.Count;
                    }
                }

                if (response.Districts != null)
                {
                    foreach (var item in response.Districts)
                    {
                        if (item is null)
                        {
                            continue;
                        }

                        var key = string.Join("|", new[] { item.Province, item.City, item.District }.Where(part => !string.IsNullOrWhiteSpace(part)));
                        if (string.IsNullOrEmpty(key))
                        {
                            continue;
                        }

                        districtStats.TryGetValue(key, out var count);
                        districtStats[key] = count + item.Count;
                    }
                }
            }

            return new UsageDistributionResponse
            {
                Provinces = provinceStats
                    .Select(pair => new LocationStat { Province = pair.Key, Count = pair.Value })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                Cities = cityStats
                    .Select(pair =>
                    {
                        var segments = pair.Key.Split('|');
                        return new LocationStat
                        {
                            Province = segments.Length > 0 ? segments[0] : string.Empty,
                            City = segments.Length > 1 ? segments[1] : string.Empty,
                            Count = pair.Value
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                Districts = districtStats
                    .Select(pair =>
                    {
                        var segments = pair.Key.Split('|');
                        return new LocationStat
                        {
                            Province = segments.Length > 0 ? segments[0] : string.Empty,
                            City = segments.Length > 1 ? segments[1] : string.Empty,
                            District = segments.Length > 2 ? segments[2] : string.Empty,
                            Count = pair.Value
                        };
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                ResolvedTotal = total,
            };
        }

        private static string BuildCardKey(string prefix, RandomNumberGenerator rng)
        {
            var sanitizedPrefix = prefix?.Trim() ?? string.Empty;
            if (sanitizedPrefix.Length >= RandomCardSuffixLength)
            {
                return sanitizedPrefix;
            }

            var targetLength = sanitizedPrefix.Length + RandomCardSuffixLength;
            var builder = new StringBuilder(targetLength);
            builder.Append(sanitizedPrefix);

            var buffer = new byte[RandomCardSuffixLength];
            while (builder.Length < targetLength)
            {
                rng.GetBytes(buffer);
                foreach (var b in buffer)
                {
                    if (builder.Length >= targetLength)
                    {
                        break;
                    }

                    var index = b % CardKeyAlphabet.Length;
                    builder.Append(CardKeyAlphabet[index]);
                }
            }

            return builder.ToString();
        }

        private sealed class ActivatedCardRecord
        {
            public string Card { get; set; } = string.Empty;
            public long ActivateTime { get; set; }
        }

        private sealed class ActivationTrendRow
        {
            public string Day { get; set; } = string.Empty;
            public long Count { get; set; }
            public string? Whom { get; set; }
        }

        private sealed class AgentHierarchyInfo
        {
            public string User { get; set; } = string.Empty;
            public string? FNode { get; set; }
        }

        private static string FormatActivateTime(long timestamp)
        {
            if (timestamp <= 0) return string.Empty;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp)
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd-HH:mm");
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        }


        private static CardType MapCardType(SqliteBridge.CardTypeRecord record)
        {
            return new CardType
            {
                Name = record.Name,
                Prefix = record.Prefix,
                Duration = record.Duration,
                FYI = record.Fyi,
                Price = record.Price,
                Param = record.Param,
                Bind = record.Bind,
                OpenNum = record.OpenNum,
                Remarks = record.Remarks,
                CannotBeChanged = record.CannotBeChanged != 0,
                Attr_UnBindLimitTime = record.AttrUnbindLimitTime,
                Attr_UnBindDeductTime = record.AttrUnbindDeductTime,
                Attr_UnBindFreeCount = record.AttrUnbindFreeCount,
                Attr_UnBindMaxCount = record.AttrUnbindMaxCount,
                BindIP = record.BindIp,
                BindMachineNum = record.BindMachineNum,
                LockBindPcsign = record.LockBindPcsign,
                ActivateTime_ = record.ActivateTime,
                ExpiredTime_ = record.ExpiredTime,
                LastLoginTime_ = record.LastLoginTime,
                Delstate = record.DelState,
                Cty = record.Cty != 0,
                ExpiredTime__ = record.ExpiredTime2,
            };
        }

        private static CardInfo MapCardRecord(SqliteBridge.CardRecord record)
        {
            return new CardInfo
            {
                Prefix_Name = record.PrefixName,
                Whom = record.Whom,
                CardType = record.CardType,
                FYI = record.Fyi,
                State = record.State,
                Bind = record.Bind,
                OpenNum = record.OpenNum,
                LoginCount = record.LoginCount,
                IP = record.Ip,
                Remarks = record.Remarks,
                CreateData_ = record.CreateData,
                ActivateTime_ = record.ActivateTime,
                ExpiredTime_ = record.ExpiredTime,
                LastLoginTime_ = record.LastLoginTime,
                Delstate = record.DelState,
                Price = record.Price,
                Cty = record.Cty != 0,
                ExpiredTime__ = record.ExpiredTime2,
                UnBindCount = record.UnbindCount,
                UnBindDeduct = record.UnbindDeduct,
                Attr_UnBindLimitTime = record.AttrUnbindLimitTime,
                Attr_UnBindDeductTime = record.AttrUnbindDeductTime,
                Attr_UnBindFreeCount = record.AttrUnbindFreeCount,
                Attr_UnBindMaxCount = record.AttrUnbindMaxCount,
                BindIP = record.BindIp,
                BanTime = record.BanTime,
                Owner = record.Owner,
                BindUser = record.BindUser,
                NowBindMachineNum = record.NowBindMachineNum,
                BindMachineNum = record.BindMachineNum,
                PCSign2 = record.PcSign2,
                BanDurationTime = record.BanDurationTime,
                GiveBackBanTime = record.GiveBackBanTime,
                PICXCount = record.PicxCount,
                LockBindPcsign = record.LockBindPcsign,
                LastRechargeTime = record.LastRechargeTime,
                UserExtraData = record.UserExtraData,
                MachineCodes = new List<string>()
            };
        }


        private async Task<IDictionary<string, (string Province, string City, string District)>> ResolveIpLocationsInternalAsync(
            IEnumerable<string> ips,
            string? software,
            int maxImmediateResolutions,
            bool scheduleBackground,
            CancellationToken cancellationToken)
        {
            var distinctIps = (ips ?? Array.Empty<string>())
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Select(ip => ip.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var results = new Dictionary<string, (string Province, string City, string District)>(StringComparer.OrdinalIgnoreCase);

            if (distinctIps.Count == 0)
            {
                return results;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var pending = new List<string>();

            foreach (var ip in distinctIps)
            {
                if (IpLocationCache.TryGetValue(ip, out var cachedEntry) && !IsCacheExpired(cachedEntry.UpdatedAt, now))
                {
                    results[ip] = cachedEntry.AsTuple();
                    continue;
                }

                pending.Add(ip);
            }

            if (pending.Count == 0)
            {
                return results;
            }

            var dbRecords = await _ipLocationCacheRepository
                .GetAsync(pending, cancellationToken)
                .ConfigureAwait(false);

            var expiredRecords = new Dictionary<string, IpLocationCacheEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var ip in pending)
            {
                if (!dbRecords.TryGetValue(ip, out var record))
                {
                    continue;
                }

                var normalized = NormalizeLocation(record.Province ?? string.Empty, record.City ?? string.Empty, record.District ?? string.Empty);
                if (!IsCacheExpired(record.UpdatedAt, now))
                {
                    results[ip] = normalized;
                    IpLocationCache[ip] = new CachedIpLocation
                    {
                        Province = normalized.Province,
                        City = normalized.City,
                        District = normalized.District,
                        UpdatedAt = record.UpdatedAt
                    };
                }
                else
                {
                    expiredRecords[ip] = record;
                }
            }

            var toResolve = pending.Where(ip => !results.ContainsKey(ip)).ToList();
            var backgroundIps = new List<string>();

            if (toResolve.Count > 0 && maxImmediateResolutions >= 0 && toResolve.Count > maxImmediateResolutions)
            {
                if (scheduleBackground && !string.IsNullOrWhiteSpace(software))
                {
                    backgroundIps = toResolve.Skip(maxImmediateResolutions).ToList();
                }

                toResolve = maxImmediateResolutions == 0
                    ? new List<string>()
                    : toResolve.Take(maxImmediateResolutions).ToList();
            }

            if (toResolve.Count > 0)
            {
                var resolutionTasks = toResolve
                    .Select(ip => ResolveIpWithConcurrencyAsync(ip, expiredRecords.TryGetValue(ip, out var record) ? record : null))
                    .ToList();

                var resolvedResults = await Task.WhenAll(resolutionTasks).ConfigureAwait(false);
                var upsertRecords = new List<IpLocationCacheEntry>();

                foreach (var resolved in resolvedResults)
                {
                    var ip = resolved.Ip;
                    var location = resolved.Location;
                    var updatedAt = resolved.UpdatedAt;

                    results[ip] = location;

                    IpLocationCache[ip] = new CachedIpLocation
                    {
                        Province = location.Province,
                        City = location.City,
                        District = location.District,
                        UpdatedAt = updatedAt
                    };

                    upsertRecords.Add(new IpLocationCacheEntry()
                    {
                        Ip = ip,
                        Province = location.Province ?? string.Empty,
                        City = location.City ?? string.Empty,
                        District = location.District ?? string.Empty,
                        UpdatedAt = updatedAt
                    });
                }

                if (upsertRecords.Count > 0)
                {
                    await _ipLocationCacheRepository.UpsertAsync(upsertRecords, cancellationToken).ConfigureAwait(false);
                }
            }

            if (scheduleBackground && backgroundIps.Count > 0 && !string.IsNullOrWhiteSpace(software))
            {
                QueueBackgroundIpResolution(software, backgroundIps);
            }

            return results;
        }

        private void QueueBackgroundIpResolution(string software, IList<string> ips)
        {
            var uniqueIps = ips
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Select(ip => ip.Trim())
                .Where(ip => BackgroundIpRefreshSet.TryAdd(ip, 0))
                .ToList();

            if (uniqueIps.Count == 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await ResolveIpLocationsInternalAsync(uniqueIps, null, int.MaxValue, scheduleBackground: false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "后台刷新 IP 地址定位失败");
                }
                finally
                {
                    foreach (var ip in uniqueIps)
                    {
                        BackgroundIpRefreshSet.TryRemove(ip, out _);
                    }
                }
            });
        }

        private async Task<(string Ip, (string Province, string City, string District) Location, long UpdatedAt)> ResolveIpWithConcurrencyAsync(string ip, IpLocationCacheEntry? fallback)
        {
            await IpResolverSemaphore.WaitAsync();
            try
            {
                var location = await ResolveLocationWithResolversAsync(ip);
                if (!HasLocation(location) && fallback != null)
                {
                    location = NormalizeLocation(fallback.Province ?? string.Empty, fallback.City ?? string.Empty, fallback.District ?? string.Empty);
                }

                var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return (ip, location, updatedAt);
            }
            finally
            {
                IpResolverSemaphore.Release();
            }
        }

        private async Task<(string Province, string City, string District)> ResolveLocationWithResolversAsync(string ip)
        {
            foreach (var resolver in IpResolvers)
            {
                try
                {
                    var result = await resolver(ip);
                    if (HasLocation(result))
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve IP location for {Ip} using {Resolver}", ip, resolver.Method.Name);
                }
            }

            return EmptyLocation;
        }

        private static (string Province, string City, string District) ParseChinaLocation(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var normalized = Regex.Replace(raw, @"[^\p{IsCJKUnifiedIdeographs}]", string.Empty);
            if (string.IsNullOrEmpty(normalized))
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var province = ExtractProvince(normalized);
            var remainder = normalized;

            if (!string.IsNullOrEmpty(province))
            {
                var provinceIndex = remainder.IndexOf(province, StringComparison.Ordinal);
                if (provinceIndex >= 0)
                {
                    remainder = remainder.Substring(provinceIndex + province.Length);
                }
            }

            var city = ExtractCity(remainder, province);
            if (!string.IsNullOrEmpty(city))
            {
                var cityIndex = remainder.IndexOf(city, StringComparison.Ordinal);
                if (cityIndex >= 0)
                {
                    remainder = remainder.Substring(cityIndex + city.Length);
                }
            }

            var district = ExtractDistrict(remainder);

            return (province, city, district);
        }

        private static string ExtractProvince(string input)
        {
            foreach (var province in ChinaProvinces)
            {
                if (input.IndexOf(province, StringComparison.Ordinal) >= 0)
                {
                    return province;
                }

                var alias = ShortenProvinceName(province);
                if (!string.IsNullOrEmpty(alias) && input.IndexOf(alias, StringComparison.Ordinal) >= 0)
                {
                    return province;
                }
            }

            var match = Regex.Match(input, @"([\p{IsCJKUnifiedIdeographs}]{2,}?)(省|自治区|特别行政区)");
            if (match.Success)
            {
                return match.Groups[1].Value + match.Groups[2].Value;
            }

            return string.Empty;
        }

        private static string ExtractCity(string input, string province)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var match = Regex.Match(input, @"([\p{IsCJKUnifiedIdeographs}]{2,}?)(市|地区|盟|州)");
            if (match.Success)
            {
                return match.Groups[1].Value + match.Groups[2].Value;
            }

            if (!string.IsNullOrWhiteSpace(province) && DirectMunicipalities.Contains(province))
            {
                return province;
            }

            return string.Empty;
        }

        private static string ExtractDistrict(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var match = Regex.Match(input, @"([\p{IsCJKUnifiedIdeographs}]{1,}?)(区|县|旗|市)");
            if (match.Success)
            {
                return match.Groups[1].Value + match.Groups[2].Value;
            }

            return string.Empty;
        }

        private static string ExtractIpAddress(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var candidates = raw
                .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var candidate in candidates)
            {
                if (!IPAddress.TryParse(candidate, out var address))
                {
                    continue;
                }

                if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IsPrivateIpv4(address))
                {
                    continue;
                }

                return address.ToString();
            }

            return string.Empty;
        }

        private static bool HasLocation((string Province, string City, string District) location)
        {
            return !string.IsNullOrWhiteSpace(location.Province)
                   || !string.IsNullOrWhiteSpace(location.City)
                   || !string.IsNullOrWhiteSpace(location.District);
        }

        private static (string Province, string City, string District) NormalizeLocation(string province, string city, string district)
        {
            province = province?.Trim() ?? string.Empty;
            city = city?.Trim() ?? string.Empty;
            district = district?.Trim() ?? string.Empty;

            var combined = string.Concat(province, city, district);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                var parsed = ParseChinaLocation(combined);
                if (HasLocation(parsed))
                {
                    return (
                        string.IsNullOrWhiteSpace(parsed.Province) ? province : parsed.Province,
                        string.IsNullOrWhiteSpace(parsed.City) ? city : parsed.City,
                        string.IsNullOrWhiteSpace(parsed.District) ? district : parsed.District);
                }
            }

            return (province, city, district);
        }

        private static bool IsCacheExpired(long updatedAt, long now)
        {
            if (updatedAt <= 0)
            {
                return true;
            }

            return now - updatedAt >= IpLocationCacheTtlSeconds;
        }

        private static bool IsPrivateIpv4(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();

            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SProtectAgent/1.0");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static async Task<(string Province, string City, string District)> QueryLocationFromPcOnlineAsync(string ip)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://whois.pconline.com.cn/ipJson.jsp?json=true&ip={Uri.EscapeDataString(ip)}");
                request.Headers.Referrer = PcOnlineReferer;

                using var response = await HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return EmptyLocation;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var payload = Encoding.GetEncoding("GB18030").GetString(bytes);

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                if (root.TryGetProperty("err", out var errorElement) && !string.IsNullOrWhiteSpace(errorElement.GetString()))
                {
                    return EmptyLocation;
                }

                var province = root.TryGetProperty("pro", out var provinceElement) ? provinceElement.GetString() ?? string.Empty : string.Empty;
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() ?? string.Empty : string.Empty;
                var district = root.TryGetProperty("region", out var districtElement) ? districtElement.GetString() ?? string.Empty : string.Empty;

                return NormalizeLocation(province, city, district);
            }
            catch
            {
                return EmptyLocation;
            }
        }

        private static async Task<(string Province, string City, string District)> QueryLocationFromIpApiAsync(string ip)
        {
            try
            {
                using var response = await HttpClient.GetAsync($"http://ip-api.com/json/{Uri.EscapeDataString(ip)}?lang=zh-CN&fields=status,message,regionName,city,district");
                if (!response.IsSuccessStatusCode)
                {
                    return EmptyLocation;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                var root = document.RootElement;

                if (!root.TryGetProperty("status", out var statusElement) || !string.Equals(statusElement.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    return EmptyLocation;
                }

                var province = root.TryGetProperty("regionName", out var regionElement) ? regionElement.GetString() ?? string.Empty : string.Empty;
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() ?? string.Empty : string.Empty;
                var district = root.TryGetProperty("district", out var districtElement) ? districtElement.GetString() ?? string.Empty : string.Empty;

                return NormalizeLocation(province, city, district);
            }
            catch
            {
                return EmptyLocation;
            }
        }

        private static string ShortenProvinceName(string province)
        {
            if (province.EndsWith("省", StringComparison.Ordinal) || province.EndsWith("市", StringComparison.Ordinal))
            {
                return province.Substring(0, province.Length - 1);
            }

            if (province.EndsWith("自治区", StringComparison.Ordinal))
            {
                return province.Substring(0, province.Length - 3);
            }

            if (province.EndsWith("特别行政区", StringComparison.Ordinal))
            {
                return province.Substring(0, province.Length - 5);
            }

            return string.Empty;
        }

        private static readonly string[] ChinaProvinces =
        {
            "北京市", "天津市", "上海市", "重庆市",
            "河北省", "山西省", "辽宁省", "吉林省", "黑龙江省",
            "江苏省", "浙江省", "安徽省", "福建省", "江西省",
            "山东省", "河南省", "湖北省", "湖南省", "广东省",
            "广西壮族自治区", "海南省", "四川省", "贵州省", "云南省",
            "西藏自治区", "陕西省", "甘肃省", "青海省", "宁夏回族自治区",
            "新疆维吾尔自治区", "内蒙古自治区", "香港特别行政区", "澳门特别行政区", "台湾省"
        };

        private static readonly HashSet<string> DirectMunicipalities = new HashSet<string>(StringComparer.Ordinal)
        {
            "北京市", "天津市", "上海市", "重庆市"
        };

    }
}

