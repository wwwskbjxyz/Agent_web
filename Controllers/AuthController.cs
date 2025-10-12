using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>
/// 身份验证与会话管理接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly SessionManager _sessionManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AdminPermissionHelper _adminPermissionHelper;

    public AuthController(AuthService authService, SessionManager sessionManager, JwtTokenService jwtTokenService, AdminPermissionHelper adminPermissionHelper)
    {
        _authService = authService;
        _sessionManager = sessionManager;
        _jwtTokenService = jwtTokenService;
        _adminPermissionHelper = adminPermissionHelper;
    }

    /// <summary>
    /// 登录并创建会话。
    /// </summary>
    /// <param name="request">包含用户名与密码的登录信息。</param>
    /// <returns>登录结果与可访问软件列表。</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam));
        }

            try
            {
                var session = await _authService.CreateUserSessionAsync(request.Username, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent);
                session.IsSuper = _adminPermissionHelper.HasSuperPermission(session.Username);
                var token = _jwtTokenService.CreateAccessToken(session);
                _sessionManager.SetUserSession(session, token.TokenId, token.ExpiresAtUtc);

                var response = new LoginResponse
                {
                    Username = session.Username,
                    SoftwareList = session.SoftwareList,
                    Token = token.Token,
                    ExpiresAt = token.ExpiresAtUtc,
                    IsSuper = session.IsSuper
                };

            return Ok(ApiResponse.Success(response, "登录成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidCredentials, ex.Message));
        }
    }

    /// <summary>
    /// 获取当前登录用户的会话信息。
    /// </summary>
    /// <returns>当前用户名及可访问软件列表。</returns>
    [HttpPost("getUserInfo")]
    public IActionResult GetUserInfo()
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        var response = new RefreshUserInfoResponse
        {
            Username = session.Username,
            SoftwareList = session.SoftwareList,
            SoftwareAgentInfo = session.SoftwareAgentInfo,
            TokenExpiresAt = _sessionManager.GetCurrentTokenExpiration(),
            IsSuper = session.IsSuper || _adminPermissionHelper.HasSuperPermission(session.Username)
        };

        return Ok(ApiResponse.Success(response));
    }

    /// <summary>
    /// 刷新当前登录用户的会话信息。
    /// </summary>
    /// <returns>刷新后的用户信息。</returns>
    [HttpPost("refreshUserInfo")]
    public async Task<IActionResult> RefreshUserInfo()
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

            try
            {
                var refreshed = await _authService.RefreshUserInfoAsync(session);
                refreshed.IsSuper = _adminPermissionHelper.HasSuperPermission(refreshed.Username);
                _sessionManager.ClearUserSession();
                var token = _jwtTokenService.CreateAccessToken(refreshed);
                _sessionManager.SetUserSession(refreshed, token.TokenId, token.ExpiresAtUtc);

                var response = new RefreshUserInfoResponse
                {
                    Username = refreshed.Username,
                    SoftwareList = refreshed.SoftwareList,
                    SoftwareAgentInfo = refreshed.SoftwareAgentInfo,
                    Token = token.Token,
                    TokenExpiresAt = token.ExpiresAtUtc,
                    IsSuper = refreshed.IsSuper
                };

            return Ok(ApiResponse.Success(response, "刷新成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InternalError, ex.Message));
        }
    }

    /// <summary>
    /// 修改当前用户在指定软件位下的登录密码。
    /// </summary>
    /// <param name="request">包含旧密码、新密码及软件位的信息。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("changePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!ModelState.IsValid)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam));
        }

        try
        {
            await _authService.ChangePasswordAsync(session.Username, request.Software, request.OldPassword, request.NewPassword);
            return Ok(ApiResponse.Success("密码修改成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InternalError, ex.Message));
        }
    }

    /// <summary>
    /// 退出登录并清理会话。
    /// </summary>
    /// <returns>操作结果。</returns>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _sessionManager.ClearUserSession();
        return Ok(ApiResponse.Success("登出成功"));
    }
}

