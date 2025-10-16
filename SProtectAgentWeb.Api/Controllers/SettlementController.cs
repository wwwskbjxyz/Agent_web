using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class SettlementController : ControllerBase
{
    private readonly SessionManager _sessionManager;
    private readonly SettlementRateService _settlementRateService;
    private readonly SettlementLifecycleService _settlementLifecycleService;
    private readonly AgentService _agentService;
    private readonly PermissionHelper _permissionHelper;
    private readonly CardService _cardService;
    private readonly ILogger<SettlementController> _logger;

    public SettlementController(
        SessionManager sessionManager,
        SettlementRateService settlementRateService,
        SettlementLifecycleService settlementLifecycleService,
        AgentService agentService,
        PermissionHelper permissionHelper,
        CardService cardService,
        ILogger<SettlementController> logger)
    {
        _sessionManager = sessionManager;
        _settlementRateService = settlementRateService;
        _settlementLifecycleService = settlementLifecycleService;
        _agentService = agentService;
        _permissionHelper = permissionHelper;
        _cardService = cardService;
        _logger = logger;
    }

    [HttpPost("list")]
    public async Task<IActionResult> GetRatesAsync([FromBody] SettlementRateListRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var resolution = await ResolveTargetAgentAsync(
                request.Software,
                agent,
                request.TargetAgent,
                includeOptions: true)
            .ConfigureAwait(false);

        if (resolution.ErrorResult != null)
        {
            return resolution.ErrorResult;
        }

        var fallbackAgent = agent.User?.Trim();

        var rates = await _settlementRateService
            .GetRatesAsync(request.Software, resolution.TargetAgent, fallbackAgent, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var details = await _settlementLifecycleService
            .GetDetailsAsync(request.Software, resolution.TargetAgent, fallbackAgent, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var agentsForReminder = new List<string> { resolution.TargetAgent };
        if (resolution.AccessibleAgents is { Count: > 0 })
        {
            agentsForReminder.AddRange(resolution.AccessibleAgents.Select(a => a.User));
        }

        var reminderMap = await _settlementLifecycleService
            .GetReminderMapAsync(request.Software, agentsForReminder, agent.User, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var billDtos = await BuildBillDtosAsync(
                request.Software,
                resolution.TargetAgent,
                details.Bills,
                HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var payload = new SettlementRateListResponse
        {
            TargetAgent = resolution.TargetAgent,
            Agents = BuildAgentOptions(agent, resolution.TargetAgent, resolution.AccessibleAgents, reminderMap),
            Rates = rates
                .Select(rate => new SettlementRateDto
                {
                    CardType = rate.CardType,
                    Price = rate.Price
                })
                .ToList(),
            Cycle = ToCycleDto(details.Cycle),
            Bills = billDtos,
            HasPendingReminder = billDtos.Any(bill => !bill.IsSettled)
        };

        return Ok(ApiResponse.Success(payload));
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> ReplaceRatesAsync([FromBody] SettlementRateUpdateRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var resolution = await ResolveTargetAgentAsync(
                request.Software,
                agent,
                request.TargetAgent,
                includeOptions: true)
            .ConfigureAwait(false);

        if (resolution.ErrorResult != null)
        {
            return resolution.ErrorResult;
        }

        var normalized = request.Rates
            .Select(rate => new SettlementRate
            {
                CardType = rate.CardType ?? string.Empty,
                Price = rate.Price
            })
            .ToArray();

        if (normalized.Any(rate => rate.Price < 0))
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "结算价格不能为负数"));
        }

        if (request.CycleTimeMinutes.HasValue)
        {
            var minutes = request.CycleTimeMinutes.Value;
            if (minutes < 0 || minutes >= 24 * 60)
            {
                return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "结算时间需在 00:00-23:59 范围内"));
            }
        }

        SettlementRateListResponse payload;
        try
        {
            await _settlementRateService
                .ReplaceRatesAsync(request.Software, resolution.TargetAgent, normalized, HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (request.CycleDays.HasValue || request.CycleTimeMinutes.HasValue)
            {
                await _settlementLifecycleService
                    .UpdateCycleAsync(
                        request.Software,
                        resolution.TargetAgent,
                        request.CycleDays,
                        request.CycleTimeMinutes,
                        HttpContext.RequestAborted)
                    .ConfigureAwait(false);
            }

            var displayRates = await _settlementRateService
                .GetRatesAsync(request.Software, resolution.TargetAgent, agent.User, HttpContext.RequestAborted)
                .ConfigureAwait(false);

            var details = await _settlementLifecycleService
                .GetDetailsAsync(request.Software, resolution.TargetAgent, agent.User, HttpContext.RequestAborted)
                .ConfigureAwait(false);

            var reminderMap = await _settlementLifecycleService
                .GetReminderMapAsync(
                    request.Software,
                    BuildReminderAgentList(resolution.TargetAgent, resolution.AccessibleAgents),
                    agent.User,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);

            var billDtos = await BuildBillDtosAsync(
                    request.Software,
                    resolution.TargetAgent,
                    details.Bills,
                    HttpContext.RequestAborted)
                .ConfigureAwait(false);

            payload = new SettlementRateListResponse
            {
                TargetAgent = resolution.TargetAgent,
                Rates = displayRates
                    .Select(rate => new SettlementRateDto
                    {
                        CardType = rate.CardType,
                        Price = rate.Price
                    })
                    .ToList(),
                Agents = BuildAgentOptions(agent, resolution.TargetAgent, resolution.AccessibleAgents, reminderMap),
                Cycle = ToCycleDto(details.Cycle),
                Bills = billDtos,
                HasPendingReminder = billDtos.Any(bill => !bill.IsSettled)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settlement rates for {Software}", request.Software);
            return Ok(ApiResponse.Error(ErrorCodes.InternalError, "保存失败，请稍后重试"));
        }

        return Ok(ApiResponse.Success(payload, "结算设置已保存"));
    }

    [HttpPost("bill/complete")]
    public async Task<IActionResult> CompleteBillAsync([FromBody] SettlementBillCompleteRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var resolution = await ResolveTargetAgentAsync(
                request.Software,
                agent,
                request.TargetAgent,
                includeOptions: false)
            .ConfigureAwait(false);

        if (resolution.ErrorResult != null)
        {
            return resolution.ErrorResult;
        }

        try
        {
            await _settlementLifecycleService.CompleteBillAsync(
                request.Software,
                resolution.TargetAgent,
                agent.User,
                request.BillId,
                request.Amount,
                request.Note,
                HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Complete settlement bill failed for {Software}/{Agent}", request.Software, resolution.TargetAgent);
            return Ok(ApiResponse.Error(ErrorCodes.InternalError, "更新账单失败，请稍后再试"));
        }

        var details = await _settlementLifecycleService
            .GetDetailsAsync(request.Software, resolution.TargetAgent, agent.User, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var billDtos = await BuildBillDtosAsync(
                request.Software,
                resolution.TargetAgent,
                details.Bills,
                HttpContext.RequestAborted)
            .ConfigureAwait(false);

        var response = new SettlementRateListResponse
        {
            TargetAgent = resolution.TargetAgent,
            Cycle = ToCycleDto(details.Cycle),
            Bills = billDtos,
            HasPendingReminder = billDtos.Any(bill => !bill.IsSettled)
        };

        return Ok(ApiResponse.Success(response, "账单状态已更新"));
    }

    private async Task<(string TargetAgent, IReadOnlyList<Agent> AccessibleAgents, IActionResult? ErrorResult)> ResolveTargetAgentAsync(
        string software,
        Agent currentAgent,
        string? requestedTarget,
        bool includeOptions)
    {
        var currentUsername = currentAgent.User?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(currentUsername))
        {
            return (string.Empty, Array.Empty<Agent>(), Ok(ApiResponse.Error(ErrorCodes.InternalError, "当前代理信息缺失")));
        }

        var normalizedRequest = (requestedTarget ?? string.Empty).Trim();
        var includeAll = _permissionHelper.HasPermission(currentAgent.Authority, 0x0000_0020);

        var targetAgent = currentAgent;

        if (!string.IsNullOrEmpty(normalizedRequest)
            && !string.Equals(normalizedRequest, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = await _agentService.FindAgentAsync(software, normalizedRequest).ConfigureAwait(false);
            if (candidate is null)
            {
                return (currentUsername, Array.Empty<Agent>(), Ok(ApiResponse.Error(ErrorCodes.AgentNotFound, "目标代理不存在")));
            }

            var allowed = includeAll
                ? candidate.IsChildOf(_permissionHelper, currentUsername)
                : candidate.IsDirectChildOf(_permissionHelper, currentUsername);

            if (!allowed)
            {
                return (currentUsername, Array.Empty<Agent>(), Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权配置该代理的结算价格")));
            }

            targetAgent = candidate;
        }

        IReadOnlyList<Agent> accessibleAgents = Array.Empty<Agent>();
        if (includeOptions)
        {
            accessibleAgents = await _agentService
                .GetAccessibleAgentsAsync(software, currentUsername, includeAll)
                .ConfigureAwait(false);
        }

        return (targetAgent.User, accessibleAgents, null);
    }

    private static IList<SettlementAgentOption> BuildAgentOptions(
        Agent currentAgent,
        string targetAgent,
        IReadOnlyList<Agent> accessibleAgents,
        IReadOnlyDictionary<string, bool> reminderMap)
    {
        var options = new List<SettlementAgentOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accessible = accessibleAgents ?? Array.Empty<Agent>();

        void AddOption(string? username, string? remark, bool isSelf)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            if (!seen.Add(username))
            {
                return;
            }

            var baseLabel = string.IsNullOrWhiteSpace(remark)
                ? username
                : $"{username}（{remark.Trim()}）";

            var display = isSelf ? $"{baseLabel} · 当前账号" : baseLabel;

            options.Add(new SettlementAgentOption
            {
                Username = username,
                DisplayName = display,
                HasPendingReminder = reminderMap.TryGetValue(username, out var hasReminder) && hasReminder
            });
        }

        AddOption(currentAgent.User, currentAgent.Remarks, isSelf: true);

        foreach (var child in accessible
                     .Where(item => !string.Equals(item.User, currentAgent.User, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(item => item.User, StringComparer.OrdinalIgnoreCase))
        {
            AddOption(child.User, child.Remarks, isSelf: false);
        }

        if (!string.IsNullOrWhiteSpace(targetAgent)
            && !options.Any(option => string.Equals(option.Username, targetAgent, StringComparison.OrdinalIgnoreCase)))
        {
            var remark = accessible
                .FirstOrDefault(agent => string.Equals(agent.User, targetAgent, StringComparison.OrdinalIgnoreCase))?.Remarks;
            AddOption(targetAgent, remark, isSelf: false);
        }

        return options;
    }

    private static SettlementCycleDto? ToCycleDto(SettlementCycleInfo? info)
    {
        if (info is null)
        {
            return null;
        }

        return new SettlementCycleDto
        {
            EffectiveDays = info.EffectiveCycleDays,
            OwnDays = info.OwnCycleDays,
            EffectiveTimeMinutes = info.EffectiveCycleTimeMinutes,
            OwnTimeMinutes = info.OwnCycleTimeMinutes,
            EffectiveTimeLabel = FormatCycleTimeLabel(info.EffectiveCycleTimeMinutes),
            OwnTimeLabel = FormatCycleTimeLabel(info.OwnCycleTimeMinutes),
            IsInherited = info.IsInherited,
            NextDueTimeUtc = info.NextDueAtUtc?.ToString("o"),
            LastSettledTimeUtc = info.LastSettledAtUtc?.ToString("o"),
            IsDue = info.IsDue
        };
    }

    private static string FormatCycleTimeLabel(int minutes)
    {
        if (minutes < 0)
        {
            minutes = 0;
        }

        var normalized = minutes % (24 * 60);
        if (normalized < 0)
        {
            normalized += 24 * 60;
        }

        var hours = normalized / 60;
        var mins = normalized % 60;
        return $"{hours:D2}:{mins:D2}";
    }

    private static IEnumerable<string> BuildReminderAgentList(string targetAgent, IReadOnlyList<Agent> accessibleAgents)
    {
        var list = new List<string> { targetAgent };
        if (accessibleAgents is { Count: > 0 })
        {
            list.AddRange(accessibleAgents.Select(agent => agent.User));
        }

        return list;
    }

    private async Task<IList<SettlementBillDto>> BuildBillDtosAsync(
        string software,
        string targetAgent,
        IReadOnlyList<SettlementBill> bills,
        CancellationToken cancellationToken)
    {
        if (bills is not { Count: > 0 })
        {
            return new List<SettlementBillDto>();
        }

        var agentMap = await BuildAgentMapAsync(software, targetAgent, cancellationToken).ConfigureAwait(false);
        var directChildren = ResolveDirectChildren(agentMap, targetAgent);

        var result = new List<SettlementBillDto>();
        foreach (var bill in bills)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var breakdowns = await BuildBreakdownsForBillAsync(
                    software,
                    targetAgent,
                    bill,
                    agentMap,
                    directChildren,
                    cancellationToken)
                .ConfigureAwait(false);

            var suggested = breakdowns.Sum(item => item.Amount);
            var hasBreakdownValue = breakdowns.Any(item => item.Amount > 0m || item.Count > 0);
            var hasValue = bill.IsSettled
                ? (bill.Amount > 0m || (suggested > 0m && hasBreakdownValue))
                : (suggested > 0m && hasBreakdownValue);

            if (!hasValue)
            {
                continue;
            }

            var dto = new SettlementBillDto
            {
                Id = bill.Id,
                CycleStartUtc = bill.CycleStartUtc.ToString("o"),
                CycleEndUtc = bill.CycleEndUtc.ToString("o"),
                Amount = bill.Amount,
                SuggestedAmount = bill.IsSettled ? null : (suggested > 0m ? suggested : null),
                IsSettled = bill.IsSettled,
                SettledAtUtc = bill.SettledAtUtc?.ToString("o"),
                Note = bill.Note,
                Breakdowns = breakdowns
            };

            result.Add(dto);
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, Agent>> BuildAgentMapAsync(
        string software,
        string targetAgent,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase);

        var targetRecord = await _agentService.FindAgentAsync(software, targetAgent).ConfigureAwait(false);
        if (targetRecord != null && !string.IsNullOrWhiteSpace(targetRecord.User))
        {
            map[targetRecord.User] = targetRecord;
        }

        var descendants = await _agentService
            .GetAccessibleAgentsAsync(software, targetAgent, includeAllDescendants: true)
            .ConfigureAwait(false);

        foreach (var agent in descendants)
        {
            if (!string.IsNullOrWhiteSpace(agent.User))
            {
                map[agent.User] = agent;
            }
        }

        return map;
    }

    private IDictionary<string, Agent> ResolveDirectChildren(
        IReadOnlyDictionary<string, Agent> agentMap,
        string targetAgent)
    {
        var direct = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in agentMap)
        {
            var username = pair.Key;
            if (string.Equals(username, targetAgent, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directChild = ResolveDirectChild(username, targetAgent, agentMap);
            if (string.IsNullOrWhiteSpace(directChild))
            {
                continue;
            }

            if (!direct.ContainsKey(directChild)
                && agentMap.TryGetValue(directChild, out var agent))
            {
                direct[directChild] = agent;
            }
        }

        return direct;
    }

    private async Task<List<SettlementBillBreakdownDto>> BuildBreakdownsForBillAsync(
        string software,
        string targetAgent,
        SettlementBill bill,
        IReadOnlyDictionary<string, Agent> agentMap,
        IDictionary<string, Agent> directChildren,
        CancellationToken cancellationToken)
    {
        var breakdowns = new List<SettlementBillBreakdownDto>();

        var ownStats = await QuerySettlementStatsAsync(
                software,
                targetAgent,
                targetAgent,
                includeDescendants: false,
                bill,
                cancellationToken)
            .ConfigureAwait(false);

        if (ownStats.Amount > 0m || ownStats.Count > 0)
        {
            var display = FormatAgentDisplayName(agentMap.TryGetValue(targetAgent, out var agent) ? agent : null);
            breakdowns.Add(new SettlementBillBreakdownDto
            {
                Agent = targetAgent,
                DisplayName = display,
                Amount = ownStats.Amount,
                Count = ownStats.Count
            });
        }

        foreach (var pair in directChildren.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stats = await QuerySettlementStatsAsync(
                    software,
                    targetAgent,
                    pair.Key,
                    includeDescendants: true,
                    bill,
                    cancellationToken)
                .ConfigureAwait(false);

            if (stats.Amount <= 0m && stats.Count <= 0)
            {
                continue;
            }

            breakdowns.Add(new SettlementBillBreakdownDto
            {
                Agent = pair.Key,
                DisplayName = FormatAgentDisplayName(pair.Value),
                Amount = stats.Amount,
                Count = stats.Count
            });
        }

        return breakdowns;
    }

    private async Task<(long Count, decimal Amount)> QuerySettlementStatsAsync(
        string software,
        string targetAgent,
        string sourceAgent,
        bool includeDescendants,
        SettlementBill bill,
        CancellationToken cancellationToken)
    {
        try
        {
            var start = ToUnixSeconds(bill.CycleStartUtc);
            var rawEnd = ToUnixSeconds(bill.CycleEndUtc.AddSeconds(-1));
            var end = rawEnd < start ? start : rawEnd;

            var query = new ActivatedCardCountQuery
            {
                Software = software,
                Status = "启用",
                StartTime = start,
                EndTime = end,
                CurrentAgent = targetAgent,
                WhomList = new List<string> { sourceAgent },
                IncludeDescendants = includeDescendants
            };

            var response = await _cardService
                .CountActivatedCardsAsync(query, cancellationToken)
                .ConfigureAwait(false);

            return (response?.Count ?? 0, response?.TotalAmount ?? 0m);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to build settlement breakdown for {Software}/{Agent} in bill {Bill}",
                software,
                sourceAgent,
                bill.Id);
            return (0, 0m);
        }
    }

    private static long ToUnixSeconds(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private string FormatAgentDisplayName(Agent? agent)
    {
        if (agent is null)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(agent.Remarks))
        {
            return agent.User;
        }

        return $"{agent.User}（{agent.Remarks.Trim()}）";
    }

    private string? ResolveDirectChild(
        string candidate,
        string targetAgent,
        IReadOnlyDictionary<string, Agent> agentMap)
    {
        if (string.Equals(candidate, targetAgent, StringComparison.OrdinalIgnoreCase))
        {
            return targetAgent;
        }

        var current = candidate?.Trim();
        var normalizedTarget = targetAgent?.Trim() ?? string.Empty;

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (!agentMap.TryGetValue(current, out var info))
            {
                return null;
            }

            var parent = _permissionHelper.GetAgentParent(info.FNode);
            if (string.IsNullOrWhiteSpace(parent))
            {
                return null;
            }

            if (parent.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            current = parent.Trim();
        }

        return null;
    }
}
