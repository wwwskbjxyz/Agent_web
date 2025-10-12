using System;
using System.IO;
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
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly SessionManager _sessionManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, SessionManager sessionManager, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] string software)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        try
        {
            var conversations = await _chatService.GetConversationsAsync(software, agent.User).ConfigureAwait(false);
            return Ok(ApiResponse.Success(conversations));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string software)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        try
        {
            var contacts = await _chatService.GetContactsAsync(software, agent).ConfigureAwait(false);
            return Ok(ApiResponse.Success(contacts));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string software, [FromQuery] string conversationId, [FromQuery] string? after = null, [FromQuery] int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少会话标识"));
        }

        limit = Math.Clamp(limit, 1, 200);

        DateTimeOffset? afterTimestamp = null;
        if (!string.IsNullOrWhiteSpace(after) && DateTimeOffset.TryParse(after, out var parsed))
        {
            afterTimestamp = parsed;
        }

        try
        {
            var messages = await _chatService.GetMessagesAsync(software, conversationId, agent.User, afterTimestamp, limit).ConfigureAwait(false);
            return Ok(ApiResponse.Success(messages));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("attachment")]
    public async Task<IActionResult> GetAttachment([FromQuery] string software, [FromQuery] string conversationId, [FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(software) || string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少必要参数"));
        }

        var (session, agent) = ResolveAgent(software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Forbid();
        }

        try
        {
            var attachment = await _chatService.OpenAttachmentAsync(software, conversationId, agent.User, file).ConfigureAwait(false);
            if (attachment is null)
            {
                return NotFound();
            }

            return File(attachment.Stream, attachment.ContentType, attachment.FileName);
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "请求体为空"));
        }

        if (string.IsNullOrWhiteSpace(request.Software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(request.Software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var messageType = string.IsNullOrWhiteSpace(request.MessageType)
            ? null
            : request.MessageType?.Trim();
        var normalizedType = (messageType ?? string.Empty).Trim().ToLowerInvariant();
        var isImage = normalizedType == "image";

        if (!isImage && string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "消息内容不能为空"));
        }

        if (isImage && string.IsNullOrWhiteSpace(request.MediaBase64))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少图片数据"));
        }

        try
        {
            ChatMessagesResponse response;
            if (!string.IsNullOrWhiteSpace(request.TargetUser))
            {
                response = await _chatService.SendDirectMessageAsync(
                    request.Software,
                    agent,
                    request.TargetUser!,
                    request.Message ?? string.Empty,
                    messageType,
                    request.MediaBase64,
                    request.MediaName).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                response = await _chatService.SendMessageToConversationAsync(
                    request.Software,
                    agent,
                    request.ConversationId!,
                    request.Message ?? string.Empty,
                    messageType,
                    request.MediaBase64,
                    request.MediaName).ConfigureAwait(false);
            }
            else
            {
                return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少聊天对象信息"));
            }

            return Ok(ApiResponse.Success(response));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateChatGroupRequest request)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "请求体为空"));
        }

        if (string.IsNullOrWhiteSpace(request.Software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(request.Software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        try
        {
            var group = await _chatService.CreateGroupAsync(request.Software, agent, request.Name, request.Participants).ConfigureAwait(false);
            return Ok(ApiResponse.Success(group));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpPost("groups/{conversationId}/invite")]
    public async Task<IActionResult> InviteToGroup(string conversationId, [FromBody] InviteToChatGroupRequest request)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "请求体为空"));
        }

        if (string.IsNullOrWhiteSpace(request.Software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(request.Software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少会话标识"));
        }

        try
        {
            var group = await _chatService.InviteToGroupAsync(request.Software, agent, conversationId, request.Participants).ConfigureAwait(false);
            return Ok(ApiResponse.Success(group));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var session = _sessionManager.GetUserSession();
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        var settings = _chatService.GetSettings();
        return Ok(ApiResponse.Success(settings));
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnread([FromQuery] string software)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, "缺少软件位标识"));
        }

        var (session, agent) = ResolveAgent(software);
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (agent is null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        try
        {
            var count = await _chatService.GetUnreadCountAsync(software, agent.User).ConfigureAwait(false);
            return Ok(ApiResponse.Success(new ChatUnreadResponse { Count = count }));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    [HttpGet("unreadTotal")]
    public async Task<IActionResult> GetUnreadTotal()
    {
        var session = _sessionManager.GetUserSession();
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        try
        {
            var count = await _chatService.GetUnreadTotalAsync(session).ConfigureAwait(false);
            return Ok(ApiResponse.Success(new ChatUnreadResponse { Count = count }));
        }
        catch (Exception ex) when (HandleException(ex, out var result))
        {
            return result;
        }
    }

    private (UserSession? session, Agent? agent) ResolveAgent(string? software)
    {
        var session = _sessionManager.GetUserSession();
        if (session is null || string.IsNullOrWhiteSpace(software))
        {
            return (session, null);
        }

        return session.SoftwareAgentInfo.TryGetValue(software, out var agent)
            ? (session, agent)
            : (session, null);
    }

    private bool HandleException(Exception exception, out IActionResult result)
    {
        switch (exception)
        {
            case InvalidOperationException ex:
                result = Ok(ApiResponse.Error(ErrorCodes.InvalidRequest, ex.Message));
                return true;
            case ArgumentException ex:
                result = BadRequest(ApiResponse.Error(ErrorCodes.InvalidParam, ex.Message));
                return true;
            case DllNotFoundException ex:
                _logger.LogError(ex, "聊天功能依赖的原生组件不可用");
                result = StatusCode(500, ApiResponse.Error(ErrorCodes.InternalError, ex.Message));
                return true;
            case EntryPointNotFoundException ex:
                _logger.LogError(ex, "聊天功能依赖的原生组件不可用");
                result = StatusCode(500, ApiResponse.Error(ErrorCodes.InternalError, ex.Message));
                return true;
            case IOException ex:
                _logger.LogWarning(ex, "聊天存储访问失败");
                result = StatusCode(500, ApiResponse.Error(ErrorCodes.InternalError, "聊天存储不可用"));
                return true;
            default:
                _logger.LogError(exception, "聊天接口执行失败");
                result = StatusCode(500, ApiResponse.Error(ErrorCodes.InternalError, "服务器内部错误"));
                return true;
        }
    }
}
