using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>
/// 代理管理相关接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AgentController : ControllerBase
{
    private readonly AgentService _agentService;
    private readonly SessionManager _sessionManager;
    private readonly PermissionHelper _permissionHelper;
    private readonly AdminPermissionHelper _adminPermissionHelper;

    public AgentController(
        AgentService agentService,
        SessionManager sessionManager,
        PermissionHelper permissionHelper,
        AdminPermissionHelper adminPermissionHelper)
    {
        _agentService = agentService;
        _sessionManager = sessionManager;
        _permissionHelper = permissionHelper;
        _adminPermissionHelper = adminPermissionHelper;
    }

    /// <summary>
    /// 获取当前代理的基础信息、权限与统计数据。
    /// </summary>
    /// <param name="request">包含软件位标识的请求体。</param>
    /// <returns>代理详情与统计信息。</returns>
    [HttpPost("getUserInfo")]
    public async Task<IActionResult> GetAgentInfo([FromBody] AgentInfoRequest request)
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

        var response = await _agentService.GetAgentInfoAsync(request.Software, agent.User);
        return Ok(ApiResponse.Success(response));
    }

    /// <summary>
    /// 分页查询子代理列表。
    /// </summary>
    /// <param name="request">查询条件，例如搜索关键词、页码等。</param>
    /// <returns>子代理数据集合与总数。</returns>
    [HttpPost("getSubAgentList")]
    public async Task<IActionResult> GetSubAgentList([FromBody] SubAgentListRequest request)
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

        var authority = _permissionHelper.ParseAuthority(agent.Authority);
        var includeAll = (authority & 0x0000_0020) != 0;

        var items = await _agentService.GetSubAgentsAsync(request, agent.User, includeAll);
        var total = await _agentService.CountSubAgentsAsync(request.Software, agent.User, includeAll);

        var response = new SubAgentListResponse
        {
            Data = items,
            Total = total,
        };

        return Ok(ApiResponse.Success(response));
    }

    /// <summary>
    /// 启用指定的子代理账号。
    /// </summary>
    /// <param name="request">包含软件位及子代理用户名列表。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("enableAgent")]
    public async Task<IActionResult> EnableAgent([FromBody] ModifyAgentStatusRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.EnableAgentsAsync(request.Software, request.Username);
        return Ok(ApiResponse.Success("代理启用成功"));
    }

    /// <summary>
    /// 禁用指定的子代理账号。
    /// </summary>
    /// <param name="request">包含软件位及子代理用户名列表。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("disableAgent")]
    public async Task<IActionResult> DisableAgent([FromBody] ModifyAgentStatusRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.DisableAgentsAsync(request.Software, request.Username);
        return Ok(ApiResponse.Success("代理禁用成功"));
    }

    /// <summary>
    /// 更新子代理的备注信息。
    /// </summary>
    /// <param name="request">包含软件位、子代理用户名与备注内容。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("updateAgentRemark")]
    public async Task<IActionResult> UpdateAgentRemark([FromBody] UpdateAgentRemarkRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.UpdateRemarkAsync(request.Software, request.Username, request.Remark);
        return Ok(ApiResponse.Success("备注更新成功"));
    }

    /// <summary>
    /// 创建子代理账号。
    /// </summary>
    /// <param name="request">包含登录信息、初始余额与权限配置的请求体。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("createSubAgent")]
    public async Task<IActionResult> CreateSubAgent([FromBody] CreateAgentRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        var session = _sessionManager.GetUserSession();
        var parent = session!.SoftwareAgentInfo[request.Software];
        await _agentService.CreateSubAgentAsync(request.Software, parent.User, request);
        return Ok(ApiResponse.Success("子代理创建成功"));
    }

    /// <summary>
    /// 删除子代理账号。
    /// </summary>
    /// <param name="request">包含软件位及子代理用户名列表。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("deleteSubAgent")]
    public async Task<IActionResult> DeleteSubAgent([FromBody] DeleteAgentRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.DeleteAgentsAsync(request.Software, request.Username);
        return Ok(ApiResponse.Success("子代理删除成功"));
    }

    /// <summary>
    /// 更新子代理登录密码。
    /// </summary>
    /// <param name="request">包含软件位、子代理用户名及新密码。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("updateAgentPassword")]
    public async Task<IActionResult> UpdateAgentPassword([FromBody] UpdateAgentPasswordRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.UpdateAgentPasswordAsync(request.Software, request.Username, request.NewPassword);
        return Ok(ApiResponse.Success("代理密码已更新"));
    }

    /// <summary>
    /// 为子代理充值余额或时间库存。
    /// </summary>
    /// <param name="request">包含软件位、子代理用户名以及充值数值的请求体。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("addMoney")]
    public async Task<IActionResult> AddMoney([FromBody] AddMoneyRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0004);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        await _agentService.AddMoneyAsync(request.Software, request.Username, request.Balance, request.TimeStock);
        return Ok(ApiResponse.Success("充值成功"));
    }

    /// <summary>
    /// 查询子代理可制卡的卡密类型列表。
    /// </summary>
    /// <param name="request">包含软件位和子代理用户名。</param>
    /// <returns>卡密类型集合。</returns>
    [HttpPost("getAgentCardType")]
    public async Task<IActionResult> GetAgentCardType([FromBody] AgentTargetRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0001);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        var cardTypes = await _agentService.GetAgentCardTypesAsync(request.Software, request.Username);
        return Ok(ApiResponse.Success(new AgentCardTypeResponse { CardTypes = cardTypes }));
    }

    /// <summary>
    /// 配置子代理可制卡的卡密类型。
    /// </summary>
    /// <param name="request">包含软件位、子代理用户名及允许的卡密类型列表。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("setAgentCardType")]
    public async Task<IActionResult> SetAgentCardType([FromBody] SetAgentCardTypeRequest request)
    {
        var validationResult = ValidateAgentPermission(request.Software, 0x0000_0001);
        if (validationResult is IActionResult errorResult)
        {
            return errorResult;
        }

        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!_adminPermissionHelper.HasSuperPermission(session.Username))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "权限不足请联系管理员"));
        }

        await _agentService.SetAgentCardTypesAsync(request.Software, request.Username, request.CardTypes);
        return Ok(ApiResponse.Success("制卡权限更新成功"));
    }

    /// <summary>
    /// 校验当前会话是否具备指定的代理管理权限。
    /// </summary>
    /// <param name="software">软件位标识。</param>
    /// <param name="requiredPermission">所需权限位。</param>
    /// <returns>失败时返回错误响应，成功返回 <c>null</c>。</returns>
    private IActionResult? ValidateAgentPermission(string software, ulong requiredPermission)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.TryGetValue(software, out var agent))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var authority = _permissionHelper.ParseAuthority(agent.Authority);
        if ((authority & requiredPermission) == 0)
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "权限不足"));
        }

        return null;
    }
}

