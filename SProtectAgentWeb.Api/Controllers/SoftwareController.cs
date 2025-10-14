using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>
/// 软件位信息相关接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SoftwareController : ControllerBase
{
    private readonly SoftwareService _softwareService;
    private readonly SessionManager _sessionManager;

    public SoftwareController(SoftwareService softwareService, SessionManager sessionManager)
    {
        _softwareService = softwareService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 获取当前登录代理可访问的软件位列表。
    /// </summary>
    /// <returns>软件位集合。</returns>
    [HttpPost("GetSoftware")]
    [HttpPost("GetEnabledSoftware")]
    [HttpPost("GetSoftwareList")]
    public async Task<IActionResult> GetSoftwareList()
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        var softwares = await _softwareService.GetAccessibleSoftwareAsync(session);
        var response = new SoftwareListResponse
        {
            Softwares = softwares,
        };

        return Ok(ApiResponse.Success(response));
    }

    /// <summary>
    /// 查询指定软件位的详细信息。
    /// </summary>
    /// <param name="request">包含软件位标识的请求体。</param>
    /// <returns>软件位详情。</returns>
    [HttpPost("GetSoftwareInfo")]
    public async Task<IActionResult> GetSoftwareInfo([FromBody] SoftwareInfoRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        var info = await _softwareService.GetSoftwareInfoAsync(request.Software, session);
        if (info == null)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        return Ok(ApiResponse.Success(new SoftwareInfoResponse { Software = info }));
    }
}

