using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;   // 若命名空间不同，请改成你的 SessionManager 所在命名空间
using SProtectAgentWeb.Api.Utilities;  // 若 ApiResponse / ErrorCodes 在别处，请调整
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Controllers
{
    /// <summary>
    /// 卡密相关接口。
    /// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CardController : ControllerBase
    {
        private readonly CardService _cardService;
        private readonly SessionManager _sessionManager;

        public CardController(CardService cardService, SessionManager sessionManager)
        {
            _cardService = cardService;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// 获取卡密列表。
        /// </summary>
        [HttpPost("getCardList")]
        public async Task<IActionResult> GetCardList([FromBody] CardListRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            var query = new CardQueryParams
            {
                Software = request.Software,
                Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
                SearchType = request.SearchType,
                Keywords = NormalizeKeywords(request.Keywords),
                Page = request.Page <= 0 ? 1 : request.Page,
                PageSize = request.Limit <= 0 ? 20 : request.Limit,
                CurrentAgent = agent.User,
                WhomList = NormalizeCreators(request.Agent),
                IncludeDescendants = request.IncludeDescendants
            };

            var (items, total) = await _cardService.GetCardListAsync(query);
            var resp = new CardListResponse { Data = items, Total = total };
            return Ok(ApiResponse.Success(resp));
        }

        private static readonly char[] KeywordSeparators =
        {
            '\r', '\n', '\t', ' ', ',', ';', '、', '，', '；'
        };

        private static IList<string> NormalizeKeywords(IList<string>? rawKeywords)
        {
            if (rawKeywords is null || rawKeywords.Count == 0)
            {
                return Array.Empty<string>();
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            foreach (var entry in rawKeywords)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var segments = entry
                    .Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    var trimmed = segment.Trim();
                    if (trimmed.Length > 0)
                    {
                        if (set.Add(trimmed))
                        {
                            ordered.Add(trimmed);
                        }
                    }
                }
            }

            if (ordered.Count == 0)
            {
                return Array.Empty<string>();
            }

            return ordered;
        }

        /// <summary>
        /// 启用指定卡密。
        /// </summary>
        [HttpPost("enableCard")]
        public async Task<IActionResult> EnableCard([FromBody] ModifyCardStatusRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            await _cardService.EnableCardAsync(request.Software, request.CardKey);
            return Ok(ApiResponse.Success<object?>(null, "已启用"));
        }

        /// <summary>
        /// 禁用指定卡密。
        /// </summary>
        [HttpPost("disableCard")]
        public async Task<IActionResult> DisableCard([FromBody] ModifyCardStatusRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            await _cardService.DisableCardAsync(request.Software, request.CardKey);
            return Ok(ApiResponse.Success<object?>(null, "已禁用"));

        }

        /// <summary>
        /// 解绑指定卡密绑定的机器信息。
        /// </summary>
        [HttpPost("unbindCard")]
        public async Task<IActionResult> UnbindCard([FromBody] ModifyCardStatusRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            var affected = await _cardService.UnbindCardAsync(request.Software, request.CardKey);
            var message = affected > 0 ? "解绑成功" : "无需解绑";
            return Ok(ApiResponse.Success<object?>(null, message));
        }

        /// <summary>
        /// 启用卡密并清空封禁信息。
        /// </summary>
        [HttpPost("enableCardWithBanTimeReturn")]
        public async Task<IActionResult> EnableCardWithBanTimeReturn([FromBody] ModifyCardStatusRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            await _cardService.EnableCardWithBanTimeReturnAsync(request.Software, request.CardKey);
            return Ok(ApiResponse.Success<object?>(null, "已启用并清空封禁信息"));

        }

        /// <summary>
        /// 批量生成卡密。
        /// </summary>
        [HttpPost("generateCards")]
        public async Task<IActionResult> GenerateCards([FromBody] GenerateCardsRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            // 这里没有做余额等业务校验，如需要按你项目逻辑添加
            var result = await _cardService.GenerateCardsAsync(request.Software, agent.User, request);
            return Ok(ApiResponse.Success(result, "卡密生成成功"));
        }

        /// <summary>
        /// 查询激活卡密数量及明细。
        /// </summary>
        [HttpPost("countActivatedCards")]
        public async Task<IActionResult> CountActivatedCards([FromBody] ActivatedCardCountRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            if (!TryParseTimeParameter(request.StartTime, out var startTime, out var startError))
                return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, startError ?? "时间格式不正确"));

            if (!TryParseTimeParameter(request.EndTime, out var endTime, out var endError))
                return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, endError ?? "时间格式不正确"));

            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
                return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "时间范围不合法"));

            var creators = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Whom)) creators.Add(request.Whom);
            if (request.WhomList is { Count: > 0 }) creators.AddRange(request.WhomList);
            var normalizedCreators = creators
                .Select(n => n?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(n => n!)
                .ToList();

            var query = new ActivatedCardCountQuery
            {
                Software = request.Software,
                CardTypes = request.CardTypes ?? new List<string>(),
                Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
                StartTime = startTime,
                EndTime = endTime,
                CurrentAgent = agent.User,
                WhomList = normalizedCreators,
                IncludeDescendants = request.IncludeDescendants,
            };

            var resp = await _cardService.CountActivatedCardsAsync(query);
            return Ok(ApiResponse.Success(resp));
        }



        /// <summary>
        /// 最近七天激活趋势。
        /// </summary>
        [HttpPost("getRecentActivationTrend")]
        public async Task<IActionResult> GetRecentActivationTrend([FromBody] RecentActivationTrendRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            var resp = await _cardService.GetRecentActivationTrendAsync(
                request.Software,
                agent.User,
                request.IncludeDescendants,
                request.OnlyDescendants);
            return Ok(ApiResponse.Success(resp));
        }

        /// <summary>
        /// 卡密使用地区分布。
        /// </summary>
        [HttpPost("getUsageDistribution")]
        public async Task<IActionResult> GetUsageDistribution([FromBody] UsageDistributionRequest request)
        {
            var session = _sessionManager.GetUserSession();
            if (session == null)
                return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));

            if (!session.SoftwareAgentInfo.TryGetValue(request.Software, out var agent))
                return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));

            var data = await _cardService.GetUsageDistributionAsync(request.Software, agent.User, request.IncludeDescendants);
            return Ok(ApiResponse.Success(data));
        }

        // ---- helpers ----

        private static IList<string> NormalizeCreators(string agentField)
        {
            // 兼容老入参：Agent="0" 表示默认当前登录代理；非 "0" 则指定某个制卡人
            if (string.IsNullOrWhiteSpace(agentField) || agentField == "0")
                return new List<string>();
            return new List<string> { agentField.Trim() };
        }

        private static bool TryParseTimeParameter(string? input, out long? timestamp, out string? errorMessage)
        {
            timestamp = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(input))
                return true;

            var trimmed = input.Trim();
            if (long.TryParse(trimmed, out var unixSeconds))
            {
                timestamp = unixSeconds;
                return true;
            }

            var supportedFormats = new[]
            {
                "yyyy-MM-dd-HH:mm",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-ddTHH:mm",
                "yyyy/MM/dd HH:mm",
                "yyyy/MM/dd-HH:mm",
                "yyyy-MM-dd"
            };

            if (DateTimeOffset.TryParseExact(
                    trimmed, supportedFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var exact))
            {
                timestamp = exact.ToUnixTimeSeconds();
                return true;
            }

            if (DateTimeOffset.TryParse(
                    trimmed, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                timestamp = parsed.ToUnixTimeSeconds();
                return true;
            }

            errorMessage = $"无法识别的时间格式：{trimmed}";
            return false;
        }
    }
}
