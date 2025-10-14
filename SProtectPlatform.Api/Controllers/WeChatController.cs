using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Options;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[Route("api/wechat")]
public sealed class WeChatController : ControllerBase
{
    private readonly IWeChatMiniProgramService _miniProgramService;
    private readonly IWeChatBindingService _bindingService;
    private readonly IWeChatMessageService _messageService;
    private readonly IWeChatTemplateDataFactory _templateDataFactory;
    private readonly ILogger<WeChatController> _logger;
    private readonly WeChatOptions _options;

    public WeChatController(
        IWeChatMiniProgramService miniProgramService,
        IWeChatBindingService bindingService,
        IWeChatMessageService messageService,
        IWeChatTemplateDataFactory templateDataFactory,
        IOptions<WeChatOptions> options,
        ILogger<WeChatController> logger)
    {
        _miniProgramService = miniProgramService;
        _bindingService = bindingService;
        _messageService = messageService;
        _templateDataFactory = templateDataFactory;
        _options = options.Value;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<WeChatTemplateConfigDto>>> GetTemplates(CancellationToken cancellationToken)
    {
        static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var instant = Normalize(_options.Templates.InstantCommunication);
        var blacklist = Normalize(_options.Templates.BlacklistAlert);
        var settlement = Normalize(_options.Templates.SettlementNotice);

        var previews = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        AddPreviewIfAny(previews, WeChatTemplateKeys.InstantCommunication, _options.Previews.GetForTemplate(WeChatTemplateKeys.InstantCommunication));
        AddPreviewIfAny(previews, WeChatTemplateKeys.BlacklistAlert, _options.Previews.GetForTemplate(WeChatTemplateKeys.BlacklistAlert));
        AddPreviewIfAny(previews, WeChatTemplateKeys.SettlementNotice, _options.Previews.GetForTemplate(WeChatTemplateKeys.SettlementNotice));

        if (User?.Identity?.IsAuthenticated == true && TryResolveCurrentUser(out var userType, out var userId))
        {
            foreach (var key in new[]
                     {
                         WeChatTemplateKeys.InstantCommunication,
                         WeChatTemplateKeys.BlacklistAlert,
                         WeChatTemplateKeys.SettlementNotice
                     })
            {
                var dynamicPreview = await _templateDataFactory
                    .ResolveAsync(key, userType, userId, cancellationToken)
                    .ConfigureAwait(false);

                if (dynamicPreview.Count == 0)
                {
                    continue;
                }

                if (previews.TryGetValue(key, out var existing))
                {
                    var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in dynamicPreview)
                    {
                        merged[pair.Key] = pair.Value;
                    }

                    previews[key] = merged;
                }
                else
                {
                    previews[key] = new Dictionary<string, string>(dynamicPreview, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        var templates = new WeChatTemplateConfigDto(instant, blacklist, settlement, previews);

        return Ok(ApiResponse<WeChatTemplateConfigDto>.Success(templates));
    }

    private static void AddPreviewIfAny(IDictionary<string, IReadOnlyDictionary<string, string>> previews, string key, IReadOnlyDictionary<string, string>? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return;
        }

        previews[key] = payload;
    }

    [Authorize]
    [HttpGet("binding")]
    public async Task<ActionResult<ApiResponse<WeChatBindingDto?>>> GetBindingAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUser(out var userType, out var userId))
        {
            return Unauthorized(ApiResponse<WeChatBindingDto?>.Failure("未识别的用户类型", 401));
        }

        var binding = await _bindingService.GetBindingAsync(userType, userId, cancellationToken);
        if (binding is null)
        {
            return Ok(ApiResponse<WeChatBindingDto?>.Success(null, "未绑定"));
        }

        var dto = new WeChatBindingDto(binding.OpenId, binding.UnionId, binding.UserType, binding.UserId, binding.Nickname);
        return Ok(ApiResponse<WeChatBindingDto?>.Success(dto));
    }

    [Authorize]
    [HttpPost("bind")]
    public async Task<ActionResult<ApiResponse<WeChatBindResponse>>> BindAsync([FromBody] WeChatBindRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<WeChatBindResponse>.Failure("参数错误"));
        }

        if (!TryResolveCurrentUser(out var userType, out var userId))
        {
            return Unauthorized(ApiResponse<WeChatBindResponse>.Failure("未识别的用户类型", 401));
        }

        try
        {
            var session = await _miniProgramService.CodeToSessionAsync(request.JsCode, cancellationToken);
            var binding = await _bindingService.UpsertBindingAsync(userType, userId, session.OpenId, session.UnionId, request.Nickname, cancellationToken);
            var response = new WeChatBindResponse(binding.OpenId, binding.UnionId, binding.UserType, binding.UserId, binding.Nickname);
            return Ok(ApiResponse<WeChatBindResponse>.Success(response, "绑定成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绑定微信失败");
            return StatusCode(500, ApiResponse<WeChatBindResponse>.Failure("绑定失败，请稍后重试", 500));
        }
    }

    [Authorize]
    [HttpDelete("bind")]
    public async Task<ActionResult<ApiResponse<string>>> UnbindAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUser(out var userType, out var userId))
        {
            return Unauthorized(ApiResponse<string>.Failure("未识别的用户类型", 401));
        }

        await _bindingService.RemoveBindingAsync(userType, userId, cancellationToken);
        return Ok(ApiResponse<string>.Success("解绑成功", "解绑成功"));
    }

    [Authorize]
    [HttpPost("notify")]
    public async Task<ActionResult<ApiResponse<WeChatNotificationResultDto>>> SendAsync([FromBody] WeChatNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<WeChatNotificationResultDto>.Failure("参数错误"));
        }

        if (!TryResolveCurrentUser(out var currentUserType, out var currentUserId))
        {
            return Unauthorized(ApiResponse<WeChatNotificationResultDto>.Failure("未识别的用户类型", 401));
        }

        var targetType = request.RecipientType ?? currentUserType;
        var targetId = request.RecipientId ?? currentUserId;

        if (!string.Equals(targetType, currentUserType, StringComparison.OrdinalIgnoreCase) || targetId != currentUserId)
        {
            return Forbid();
        }

        var result = await _messageService.SendToUserAsync(request.Template, targetType, targetId, request.Data, request.Page, cancellationToken);
        return Ok(ApiResponse<WeChatNotificationResultDto>.Success(result));
    }

    private bool TryResolveCurrentUser(out string userType, out int userId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(role, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            userType = Roles.Agent;
            userId = User.GetAgentId();
            return true;
        }

        if (string.Equals(role, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            userType = Roles.Author;
            userId = User.GetAuthorId();
            return true;
        }

        userType = string.Empty;
        userId = 0;
        return false;
    }
}
