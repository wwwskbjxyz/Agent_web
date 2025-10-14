using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IBindingService _bindingService;
    private readonly IWeChatMiniProgramService _miniProgramService;
    private readonly IWeChatBindingService _wechatBindingService;
    private readonly IPasswordService _passwordService;
    private readonly IAuthorService _authorService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAgentService agentService,
        IBindingService bindingService,
        IWeChatMiniProgramService miniProgramService,
        IWeChatBindingService weChatBindingService,
        IPasswordService passwordService,
        IAuthorService authorService,
        IJwtTokenService jwtTokenService,
        ICredentialProtector credentialProtector,
        ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _bindingService = bindingService;
        _miniProgramService = miniProgramService;
        _wechatBindingService = weChatBindingService;
        _passwordService = passwordService;
        _authorService = authorService;
        _jwtTokenService = jwtTokenService;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AgentProfileDto>>> RegisterAsync([FromBody] AgentRegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AgentProfileDto>.Failure("参数错误"));
        }

        var username = request.Username.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(ApiResponse<AgentProfileDto>.Failure("用户名不能为空"));
        }

        var existingByEmail = await _agentService.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingByEmail != null)
        {
            return Conflict(ApiResponse<AgentProfileDto>.Failure("邮箱已被注册", 409));
        }

        var existingByUsername = await _agentService.GetByUsernameAsync(username, cancellationToken);
        if (existingByUsername != null)
        {
            return Conflict(ApiResponse<AgentProfileDto>.Failure("用户名已被使用", 409));
        }

        if (string.IsNullOrWhiteSpace(request.SoftwareCode))
        {
            return BadRequest(ApiResponse<AgentProfileDto>.Failure("软件码不能为空"));
        }

        var softwareCode = request.SoftwareCode.Trim().ToUpperInvariant();
        var author = await _authorService.GetBySoftwareCodeAsync(softwareCode, cancellationToken);
        if (author is null)
        {
            return NotFound(ApiResponse<AgentProfileDto>.Failure("软件码不存在", 404));
        }

        var authorAccount = request.AuthorAccount.Trim();
        if (string.IsNullOrEmpty(authorAccount))
        {
            return BadRequest(ApiResponse<AgentProfileDto>.Failure("作者账号不能为空"));
        }

        if (string.IsNullOrWhiteSpace(request.AuthorPassword))
        {
            return BadRequest(ApiResponse<AgentProfileDto>.Failure("作者密码不能为空"));
        }

        var passwordHash = _passwordService.HashPassword(normalizedEmail, request.Password);
        var agent = await _agentService.CreateAsync(username, normalizedEmail, passwordHash, username, cancellationToken);

        try
        {
            var encryptedAccount = _credentialProtector.Protect(authorAccount);
            var encryptedPassword = _credentialProtector.Protect(request.AuthorPassword);
            await _bindingService.CreateAsync(agent.Id, author.Id, author.SoftwareId, author.SoftwareCode, authorAccount, encryptedAccount, encryptedPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            await _agentService.DeleteAsync(agent.Id, cancellationToken);
            _logger.LogError(ex, "注册代理 {Email} 时创建初始绑定失败", normalizedEmail);
            return StatusCode(500, ApiResponse<AgentProfileDto>.Failure("注册失败，请稍后重试", 500));
        }

        var profile = new AgentProfileDto(agent.Username, agent.Email, agent.DisplayName);
        return Ok(ApiResponse<AgentProfileDto>.Success(profile, "注册成功"));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AgentLoginResponseDto>>> LoginAsync([FromBody] AgentLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AgentLoginResponseDto>.Failure("参数错误"));
        }

        var account = request.Account.Trim();
        if (string.IsNullOrWhiteSpace(account))
        {
            return BadRequest(ApiResponse<AgentLoginResponseDto>.Failure("账号不能为空"));
        }

        var record = await _agentService.GetAuthRecordByAccountAsync(account, cancellationToken);
        if (record == null || !_passwordService.VerifyPassword(record.Email, record.PasswordHash, request.Password))
        {
            return Unauthorized(ApiResponse<AgentLoginResponseDto>.Failure("账号或密码错误", 401));
        }

        var bindings = await _bindingService.GetBindingsForAgentAsync(record.Id, cancellationToken);
        var decryptedBindings = bindings
            .Select(binding => new BindingSummaryDto(
                binding.BindingId,
                binding.AuthorSoftwareId,
                binding.SoftwareCode,
                binding.SoftwareType,
                binding.AuthorDisplayName,
                binding.AuthorEmail,
                binding.ApiAddress,
                binding.ApiPort,
                binding.AuthorAccount,
                _credentialProtector.Unprotect(binding.EncryptedAuthorPassword)))
            .ToList();

        var (token, expiresAt) = _jwtTokenService.CreateToken(
            record.Id.ToString(),
            Roles.Agent,
            new Dictionary<string, string>
            {
                ["username"] = record.Username,
                ["displayName"] = record.DisplayName,
                ["email"] = record.Email,
                [ClaimTypes.Email] = record.Email
            });

        var response = new AgentLoginResponseDto(
            new AgentProfileDto(record.Username, record.Email, record.DisplayName),
            token,
            expiresAt,
            decryptedBindings);

        return Ok(ApiResponse<AgentLoginResponseDto>.Success(response, "登录成功"));
    }

    [HttpPost("login/wechat")]
    public async Task<ActionResult<ApiResponse<AgentLoginResponseDto>>> LoginWithWeChatAsync([FromBody] AgentWeChatLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AgentLoginResponseDto>.Failure("参数错误"));
        }

        if (string.IsNullOrWhiteSpace(request.JsCode))
        {
            return BadRequest(ApiResponse<AgentLoginResponseDto>.Failure("缺少登录凭证"));
        }

        WeChatSessionInfo sessionInfo;
        try
        {
            sessionInfo = await _miniProgramService.CodeToSessionAsync(request.JsCode.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "微信登录失败：Code2Session 异常");
            return StatusCode(502, ApiResponse<AgentLoginResponseDto>.Failure("微信登录失败，请重试", 502));
        }

        if (string.IsNullOrWhiteSpace(sessionInfo.OpenId))
        {
            return Unauthorized(ApiResponse<AgentLoginResponseDto>.Failure("未能获取微信身份", 401));
        }

        var binding = await _wechatBindingService.GetBindingByOpenIdAsync("Agent", sessionInfo.OpenId, cancellationToken);
        if (binding is null)
        {
            return NotFound(ApiResponse<AgentLoginResponseDto>.Failure("当前微信尚未绑定代理账号", 404));
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.UnionId) && !string.Equals(binding.UnionId, sessionInfo.UnionId, StringComparison.Ordinal))
        {
            binding = await _wechatBindingService.UpsertBindingAsync("Agent", binding.UserId, binding.OpenId, sessionInfo.UnionId, binding.Nickname, cancellationToken);
        }

        var record = await _agentService.GetAuthRecordByIdAsync(binding.UserId, cancellationToken);
        if (record is null)
        {
            _logger.LogWarning("微信绑定记录的代理不存在：AgentId={AgentId}", binding.UserId);
            return NotFound(ApiResponse<AgentLoginResponseDto>.Failure("账号不存在或已被删除", 404));
        }

        var bindings = await _bindingService.GetBindingsForAgentAsync(binding.UserId, cancellationToken);
        var decryptedBindings = bindings
            .Select(item => new BindingSummaryDto(
                item.BindingId,
                item.AuthorSoftwareId,
                item.SoftwareCode,
                item.SoftwareType,
                item.AuthorDisplayName,
                item.AuthorEmail,
                item.ApiAddress,
                item.ApiPort,
                item.AuthorAccount,
                _credentialProtector.Unprotect(item.EncryptedAuthorPassword)))
            .ToList();

        var (token, expiresAt) = _jwtTokenService.CreateToken(
            record.Id.ToString(CultureInfo.InvariantCulture),
            Roles.Agent,
            new Dictionary<string, string>
            {
                ["username"] = record.Username,
                ["displayName"] = record.DisplayName,
                ["email"] = record.Email,
                [ClaimTypes.Email] = record.Email
            });

        var response = new AgentLoginResponseDto(
            new AgentProfileDto(record.Username, record.Email, record.DisplayName),
            token,
            expiresAt,
            decryptedBindings);

        return Ok(ApiResponse<AgentLoginResponseDto>.Success(response, "登录成功"));
    }

    [Authorize(Roles = Roles.Agent)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<AgentLoginResponseDto>>> MeAsync(CancellationToken cancellationToken)
    {
        var agentId = User.GetAgentId();
        var record = await _agentService.GetAuthRecordByAccountAsync(User.GetEmail()!, cancellationToken);
        if (record == null)
        {
            return Unauthorized(ApiResponse<AgentLoginResponseDto>.Failure("账号不存在", 401));
        }

        var bindings = await _bindingService.GetBindingsForAgentAsync(agentId, cancellationToken);
        var decryptedBindings = bindings
            .Select(binding => new BindingSummaryDto(
                binding.BindingId,
                binding.AuthorSoftwareId,
                binding.SoftwareCode,
                binding.SoftwareType,
                binding.AuthorDisplayName,
                binding.AuthorEmail,
                binding.ApiAddress,
                binding.ApiPort,
                binding.AuthorAccount,
                _credentialProtector.Unprotect(binding.EncryptedAuthorPassword)))
            .ToList();

        var response = new AgentLoginResponseDto(
            new AgentProfileDto(record.Username, record.Email, record.DisplayName),
            string.Empty,
            DateTime.UtcNow,
            decryptedBindings);

        return Ok(ApiResponse<AgentLoginResponseDto>.Success(response));
    }
}

internal static class Roles
{
    public const string Agent = "Agent";
    public const string Author = "Author";
}

internal static class ClaimsPrincipalExtensions
{
    public static int GetAgentId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (subject == null)
        {
            throw new InvalidOperationException("Token 缺少标识");
        }

        return int.Parse(subject, CultureInfo.InvariantCulture);
    }

    public static string? GetEmail(this ClaimsPrincipal principal) => principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);

    public static int GetAuthorId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (subject == null)
        {
            throw new InvalidOperationException("Token 缺少标识");
        }

        return int.Parse(subject, CultureInfo.InvariantCulture);
    }
}
