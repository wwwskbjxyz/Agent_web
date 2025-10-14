using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/bindings")]
public sealed class BindingsController : ControllerBase
{
    private readonly IBindingService _bindingService;
    private readonly IAuthorService _authorService;
    private readonly ICredentialProtector _credentialProtector;

    public BindingsController(IBindingService bindingService, IAuthorService authorService, ICredentialProtector credentialProtector)
    {
        _bindingService = bindingService;
        _authorService = authorService;
        _credentialProtector = credentialProtector;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BindingListItemDto>>>> ListAsync(CancellationToken cancellationToken)
    {
        var agentId = User.GetAgentId();
        var bindings = await _bindingService.GetBindingsForAgentAsync(agentId, cancellationToken);
        var items = bindings.Select(binding => new BindingListItemDto(
            binding.BindingId,
            binding.AuthorSoftwareId,
            binding.SoftwareCode,
            binding.SoftwareType,
            binding.AuthorDisplayName,
            binding.AuthorEmail,
            binding.ApiAddress,
            binding.ApiPort,
            binding.AuthorAccount,
            _credentialProtector.Unprotect(binding.EncryptedAuthorPassword))).ToList();

        return Ok(ApiResponse<IReadOnlyCollection<BindingListItemDto>>.Success(items));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BindingListItemDto>>> CreateAsync([FromBody] CreateBindingRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<BindingListItemDto>.Failure("参数错误"));
        }

        var agentId = User.GetAgentId();
        var author = await _authorService.GetBySoftwareCodeAsync(request.SoftwareCode.Trim(), cancellationToken);
        if (author == null)
        {
            return NotFound(ApiResponse<BindingListItemDto>.Failure("软件码不存在", 404));
        }

        var existing = await _bindingService.GetBindingAsync(agentId, author.SoftwareCode, cancellationToken);
        if (existing != null)
        {
            return Conflict(ApiResponse<BindingListItemDto>.Failure("已绑定该软件码", 409));
        }

        var trimmedAccount = request.AuthorAccount.Trim();
        var encryptedAccount = _credentialProtector.Protect(trimmedAccount);
        var encryptedPassword = _credentialProtector.Protect(request.AuthorPassword);
        var binding = await _bindingService.CreateAsync(agentId, author.Id, author.SoftwareId, author.SoftwareCode, trimmedAccount, encryptedAccount, encryptedPassword, cancellationToken);
        var dto = new BindingListItemDto(
            binding.BindingId,
            binding.AuthorSoftwareId,
            binding.SoftwareCode,
            binding.SoftwareType,
            binding.AuthorDisplayName,
            binding.AuthorEmail,
            binding.ApiAddress,
            binding.ApiPort,
            binding.AuthorAccount,
            _credentialProtector.Unprotect(binding.EncryptedAuthorPassword));

        return Ok(ApiResponse<BindingListItemDto>.Success(dto, "绑定成功"));
    }

    [HttpDelete("{bindingId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteAsync(int bindingId, CancellationToken cancellationToken)
    {
        var agentId = User.GetAgentId();
        var deleted = await _bindingService.DeleteAsync(agentId, bindingId, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<string>.Failure("绑定不存在", 404));
        }

        return Ok(ApiResponse<string>.Success("ok", "已解除绑定"));
    }

    [HttpPut("{bindingId:int}")]
    public async Task<ActionResult<ApiResponse<BindingListItemDto>>> UpdateAsync(int bindingId, [FromBody] UpdateBindingRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<BindingListItemDto>.Failure("参数错误"));
        }

        var agentId = User.GetAgentId();
        var trimmedAccount = request.AuthorAccount.Trim();
        if (string.IsNullOrEmpty(trimmedAccount))
        {
            return BadRequest(ApiResponse<BindingListItemDto>.Failure("作者账号不能为空"));
        }

        var encryptedAccount = _credentialProtector.Protect(trimmedAccount);
        var encryptedPassword = _credentialProtector.Protect(request.AuthorPassword);
        var updated = await _bindingService.UpdateCredentialsAsync(agentId, bindingId, trimmedAccount, encryptedAccount, encryptedPassword, cancellationToken);
        if (updated is null)
        {
            return NotFound(ApiResponse<BindingListItemDto>.Failure("绑定不存在", 404));
        }

        var dto = new BindingListItemDto(
            updated.BindingId,
            updated.AuthorSoftwareId,
            updated.SoftwareCode,
            updated.SoftwareType,
            updated.AuthorDisplayName,
            updated.AuthorEmail,
            updated.ApiAddress,
            updated.ApiPort,
            updated.AuthorAccount,
            _credentialProtector.Unprotect(updated.EncryptedAuthorPassword));

        return Ok(ApiResponse<BindingListItemDto>.Success(dto, "更新成功"));
    }
}
