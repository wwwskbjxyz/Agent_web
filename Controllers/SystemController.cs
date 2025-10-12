using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>
/// 提供服务器系统状态信息。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SystemController : ControllerBase
{
    private readonly SessionManager _sessionManager;
    private readonly SystemInfoService _systemInfoService;
    private readonly AdminPermissionHelper _adminPermissionHelper;

    public SystemController(SessionManager sessionManager, SystemInfoService systemInfoService, AdminPermissionHelper adminPermissionHelper)
    {
        _sessionManager = sessionManager;
        _systemInfoService = systemInfoService;
        _adminPermissionHelper = adminPermissionHelper;
    }

    /// <summary>
    /// 获取当前服务器的系统状态，仅管理员可用。
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var session = _sessionManager.GetUserSession();
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!_adminPermissionHelper.HasSuperPermission(session.Username))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "当前权限不足可联系管理员开通"));
        }

        var data = await _systemInfoService.GetStatusAsync();
        return Ok(ApiResponse.Success<SystemStatusResponse>(data));
    }

    /// <summary>
    /// 读取当前目录下的公告文件内容。
    /// </summary>
    [HttpGet("announcement")]
    public IActionResult GetAnnouncement()
    {
        var session = _sessionManager.GetUserSession();
        if (session is null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "公告.txt");
            if (!System.IO.File.Exists(filePath))
            {
                return Ok(ApiResponse.Success(new AnnouncementResponse()));
            }

            var content = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            var info = new FileInfo(filePath);
            var updatedAt = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();

            var response = new AnnouncementResponse
            {
                Content = content,
                UpdatedAt = updatedAt
            };
            return Ok(ApiResponse.Success(response));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InternalError, $"读取公告失败：{ex.Message}"));
        }
    }
}

