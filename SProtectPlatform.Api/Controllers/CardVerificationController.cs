using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/card-verification")]
public sealed class CardVerificationController : ControllerBase
{
    private readonly ICardVerificationForwarder _forwarder;

    public CardVerificationController(ICardVerificationForwarder forwarder)
    {
        _forwarder = forwarder;
    }

    [HttpPost("verify")]
    public async Task<ActionResult<ApiResponse<CardVerificationResultDto>>> VerifyAsync(
        [FromBody] CardVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Ok(ApiResponse<CardVerificationResultDto>.Failure("参数不完整", 400));
        }

        var response = await _forwarder.VerifyAsync(request, cancellationToken);
        return Ok(response);
    }
}
