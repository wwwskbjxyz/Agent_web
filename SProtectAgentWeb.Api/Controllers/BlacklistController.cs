using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BlacklistController : ControllerBase
{
    private readonly BlacklistService _blacklistService;
    private readonly SessionManager _sessionManager;
    private readonly AdminPermissionHelper _adminPermissionHelper;

    public BlacklistController(BlacklistService blacklistService, SessionManager sessionManager, AdminPermissionHelper adminPermissionHelper)
    {
        _blacklistService = blacklistService;
        _sessionManager = sessionManager;
        _adminPermissionHelper = adminPermissionHelper;
    }

    [HttpGet("machines")]
    public async Task<IActionResult> GetMachineList([FromQuery] BlacklistQueryRequest? request)
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

        var softwareTargets = _adminPermissionHelper.HasSuperPermission(session.Username)
            ? null
            : session.SoftwareList;

        var items = await _blacklistService.GetMachineListAsync(softwareTargets, request?.Type);
        return Ok(ApiResponse.Success(new BlacklistMachineResponse { Items = items }));
    }

    [HttpPost("machines")]
    public async Task<IActionResult> AddMachine([FromBody] BlacklistUpsertRequest request)
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

        if (!ModelState.IsValid)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "参数不完整"));
        }

        try
        {
            await _blacklistService.AddMachineAsync(request.Value, request.Type, request.Remarks);
            return Ok(ApiResponse.Success<object?>(null, "已加入黑名单"));
        }
        catch (ArgumentException ex)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, ex.Message));
        }
    }

    [HttpPost("machines/delete")]
    public async Task<IActionResult> DeleteMachines([FromBody] BlacklistDeleteRequest request)
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

        if (request?.Values is null || request.Values.Count == 0)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "请选择需要删除的机器码"));
        }

        await _blacklistService.DeleteMachinesAsync(request.Values);
        return Ok(ApiResponse.Success<object?>(null, "已删除黑名单机器码"));
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] BlacklistLogQueryRequest? request)
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

        var softwareTargets = _adminPermissionHelper.HasSuperPermission(session.Username)
            ? null
            : session.SoftwareList;

        var items = await _blacklistService.GetLogsAsync(softwareTargets, request?.Limit ?? 200);
        return Ok(ApiResponse.Success(new BlacklistLogResponse { Items = items }));
    }
}

