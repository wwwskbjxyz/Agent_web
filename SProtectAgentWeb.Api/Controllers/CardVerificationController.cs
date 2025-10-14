using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardVerificationController : ControllerBase
{
    private readonly CardVerificationService _verificationService;
    private readonly ClientIpResolver _clientIpResolver;

    public CardVerificationController(CardVerificationService verificationService, ClientIpResolver clientIpResolver)
    {
        _verificationService = verificationService;
        _clientIpResolver = clientIpResolver;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] CardVerificationRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CardKey))
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "卡密不能为空"));
        }

        if (string.IsNullOrWhiteSpace(request.Software))
        {
            return Ok(ApiResponse.Error(ErrorCodes.InvalidParam, "未指定软件位"));
        }

        var ipAddress = await _clientIpResolver.ResolveAsync(HttpContext, cancellationToken);
        var result = await _verificationService.VerifyAsync(
            request.Software,
            request.CardKey,
            request.SoftwareCode,
            request.AgentAccount,
            ipAddress,
            cancellationToken);
        return Ok(ApiResponse.Success(result, result.Message));
    }
}
