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
    private readonly IBindingService _bindingService;
    private readonly IAgentService _agentService;

    public CardVerificationController(
        ICardVerificationForwarder forwarder,
        IBindingService bindingService,
        IAgentService agentService)
    {
        _forwarder = forwarder;
        _bindingService = bindingService;
        _agentService = agentService;
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

    [HttpGet("context")]
    public async Task<ActionResult<ApiResponse<CardVerificationContextDto>>> GetContextAsync(
        [FromQuery] string? softwareCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(softwareCode))
        {
            return Ok(ApiResponse<CardVerificationContextDto>.Failure("参数不完整", 400));
        }

        var trimmedCode = softwareCode.Trim();
        var binding = await _bindingService.GetBindingBySoftwareCodeAsync(trimmedCode, cancellationToken);
        if (binding is null)
        {
            return Ok(ApiResponse<CardVerificationContextDto>.Failure("未找到软件码", 404));
        }

        static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        var agent = binding.AgentId > 0
            ? await _agentService.GetByIdAsync(binding.AgentId, cancellationToken)
            : null;

        var software = Normalize(binding.SoftwareType) ?? trimmedCode;
        var displayName = Normalize(binding.AuthorDisplayName) ?? software;

        var context = new CardVerificationContextDto
        {
            Software = software,
            SoftwareCode = Normalize(binding.SoftwareCode) ?? trimmedCode,
            SoftwareDisplayName = displayName,
            AgentAccount = Normalize(agent?.Username),
            AgentDisplayName = Normalize(agent?.DisplayName)
        };

        return Ok(ApiResponse<CardVerificationContextDto>.Success(context));
    }
}
