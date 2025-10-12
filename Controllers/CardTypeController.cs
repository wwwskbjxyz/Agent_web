using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Sessions;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>
/// 卡密类型查询相关接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CardTypeController : ControllerBase
{
    private readonly CardTypeService _cardTypeService;
    private readonly SessionManager _sessionManager;

    public CardTypeController(CardTypeService cardTypeService, SessionManager sessionManager)
    {
        _cardTypeService = cardTypeService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 获取指定软件位下的卡密类型列表。
    /// </summary>
    /// <param name="request">包含软件位标识的请求体。</param>
    /// <returns>卡密类型集合。</returns>
    [HttpPost("getCardTypeList")]
    public async Task<IActionResult> GetCardTypeList([FromBody] CardTypeListRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var items = await _cardTypeService.GetCardTypesAsync(request.Software);
        return Ok(ApiResponse.Success(new CardTypeListResponse { Items = items }));
    }

    /// <summary>
    /// 根据名称查询卡密类型详情。
    /// </summary>
    /// <param name="request">包含软件位与卡密类型名称的请求体。</param>
    /// <returns>卡密类型详情。</returns>
    [HttpPost("getCardTypeByName")]
    public async Task<IActionResult> GetCardTypeByName([FromBody] GetCardTypeByNameRequest request)
    {
        var session = _sessionManager.GetUserSession();
        if (session == null)
        {
            return Unauthorized(ApiResponse.Error(ErrorCodes.TokenInvalid, "用户未登录"));
        }

        if (!session.SoftwareAgentInfo.ContainsKey(request.Software))
        {
            return Ok(ApiResponse.Error(ErrorCodes.PermissionDenied, "无权访问该软件位"));
        }

        var cardType = await _cardTypeService.GetCardTypeByNameAsync(request.Software, request.Name);
        return Ok(ApiResponse.Success(new CardTypeResponse { CardType = cardType }));
    }
}

