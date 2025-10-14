using System.Threading;
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
public class LinkAuditController : ControllerBase
{
    private readonly LinkAuditService _linkAuditService;
    private readonly SessionManager _sessionManager;
    private readonly AdminPermissionHelper _adminPermissionHelper;

    public LinkAuditController(
        LinkAuditService linkAuditService,
        SessionManager sessionManager,
        AdminPermissionHelper adminPermissionHelper)
    {
        _linkAuditService = linkAuditService;
        _sessionManager = sessionManager;
        _adminPermissionHelper = adminPermissionHelper;
    }

    [HttpPost("listLanzouLinks")]
    public async Task<IActionResult> ListLanzouLinks([FromBody] LanzouLinkListRequest request, CancellationToken cancellationToken)
    {
        var permissionCheck = EnsureSuperPermission();
        if (permissionCheck is IActionResult error)
        {
            return error;
        }

        var response = await _linkAuditService.GetLanzouLinksAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(response));
    }

    [HttpPost("deleteLanzouLinks")]
    public async Task<IActionResult> DeleteLanzouLinks([FromBody] DeleteLanzouLinksRequest request, CancellationToken cancellationToken)
    {
        var permissionCheck = EnsureSuperPermission();
        if (permissionCheck is IActionResult error)
        {
            return error;
        }

        if (request.Ids is null || request.Ids.Count == 0)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "请选择要删除的记录"));
        }

        var affected = await _linkAuditService.DeleteLanzouLinksAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(affected, affected > 0 ? "已删除" : "无可删除项"));
    }

    [HttpPost("listVerificationLogs")]
    public async Task<IActionResult> ListVerificationLogs([FromBody] CardVerificationLogListRequest request, CancellationToken cancellationToken)
    {
        var permissionCheck = EnsureSuperPermission();
        if (permissionCheck is IActionResult error)
        {
            return error;
        }

        var response = await _linkAuditService.GetVerificationLogsAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(response));
    }

    [HttpPost("deleteVerificationLogs")]
    public async Task<IActionResult> DeleteVerificationLogs([FromBody] DeleteCardVerificationLogsRequest request, CancellationToken cancellationToken)
    {
        var permissionCheck = EnsureSuperPermission();
        if (permissionCheck is IActionResult error)
        {
            return error;
        }

        if (request.Ids is null || request.Ids.Count == 0)
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "请选择要删除的记录"));
        }

        var affected = await _linkAuditService.DeleteVerificationLogsAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(affected, affected > 0 ? "已删除" : "无可删除项"));
    }

    [HttpPost("getSuspiciousCards")]
    public async Task<IActionResult> GetSuspiciousCards([FromBody] SuspiciousCardRequest request, CancellationToken cancellationToken)
    {
        var permissionCheck = EnsureSuperPermission();
        if (permissionCheck is IActionResult error)
        {
            return error;
        }

        var records = await _linkAuditService.GetSuspiciousCardsAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(records));
    }

    private IActionResult? EnsureSuperPermission()
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

        return null;
    }
}
