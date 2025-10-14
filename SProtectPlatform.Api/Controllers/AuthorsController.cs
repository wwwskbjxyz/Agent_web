using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[Route("api/authors")]
public sealed class AuthorsController : ControllerBase
{
    private readonly IAuthorService _authorService;
    private readonly IPasswordService _passwordService;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthorsController(IAuthorService authorService, IPasswordService passwordService, IJwtTokenService jwtTokenService)
    {
        _authorService = authorService;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthorProfileDto>>> RegisterAsync([FromBody] AuthorRegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AuthorProfileDto>.Failure("参数错误"));
        }

        var username = request.Username.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(ApiResponse<AuthorProfileDto>.Failure("用户名不能为空"));
        }

        var existing = await _authorService.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing != null)
        {
            return Conflict(ApiResponse<AuthorProfileDto>.Failure("邮箱已被注册", 409));
        }

        var existingByUsername = await _authorService.GetByUsernameAsync(username, cancellationToken);
        if (existingByUsername != null)
        {
            return Conflict(ApiResponse<AuthorProfileDto>.Failure("用户名已存在", 409));
        }

        var passwordHash = _passwordService.HashPassword(normalizedEmail, request.Password);
        var author = await _authorService.CreateAsync(
            username,
            normalizedEmail,
            passwordHash,
            request.DisplayName.Trim(),
            request.ApiAddress.Trim(),
            request.ApiPort,
            request.SoftwareType.Trim().ToUpperInvariant(),
            cancellationToken);

        var profile = await BuildAuthorProfileAsync(author.Id, cancellationToken);
        return Ok(ApiResponse<AuthorProfileDto>.Success(profile, "注册成功"));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthorLoginResponseDto>>> LoginAsync([FromBody] AuthorLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AuthorLoginResponseDto>.Failure("参数错误"));
        }

        var account = request.Account.Trim();
        if (string.IsNullOrWhiteSpace(account))
        {
            return BadRequest(ApiResponse<AuthorLoginResponseDto>.Failure("账号不能为空"));
        }

        var record = await _authorService.GetAuthRecordByAccountAsync(account, cancellationToken);
        if (record == null)
        {
            return Unauthorized(ApiResponse<AuthorLoginResponseDto>.Failure("账号或密码错误", 401));
        }

        if (!_passwordService.VerifyPassword(record.Email, record.PasswordHash, request.Password))
        {
            return Unauthorized(ApiResponse<AuthorLoginResponseDto>.Failure("账号或密码错误", 401));
        }

        var profile = await BuildAuthorProfileAsync(record.Id, cancellationToken);
        var additionalClaims = new Dictionary<string, string>
        {
            ["username"] = record.Username,
            ["email"] = record.Email,
            [ClaimTypes.Email] = record.Email
        };

        var (token, expiresAt) = _jwtTokenService.CreateToken(record.Id.ToString(), Roles.Author, additionalClaims);
        var response = new AuthorLoginResponseDto(profile, token, expiresAt);
        return Ok(ApiResponse<AuthorLoginResponseDto>.Success(response, "登录成功"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<AuthorProfileDto>>> MeAsync(CancellationToken cancellationToken)
    {
        var authorId = User.GetAuthorId();
        var profile = await BuildAuthorProfileAsync(authorId, cancellationToken);
        return Ok(ApiResponse<AuthorProfileDto>.Success(profile));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<AuthorProfileDto>>> UpdateAsync([FromBody] AuthorUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || request.SoftwareId <= 0)
        {
            return BadRequest(ApiResponse<AuthorProfileDto>.Failure("参数错误"));
        }

        var authorId = User.GetAuthorId();
        var updated = await _authorService.UpdateSoftwareAsync(
            authorId,
            request.SoftwareId,
            request.DisplayName.Trim(),
            request.ApiAddress.Trim(),
            request.ApiPort,
            request.SoftwareType.Trim().ToUpperInvariant(),
            cancellationToken);

        if (updated is null)
        {
            return NotFound(ApiResponse<AuthorProfileDto>.Failure("软件不存在", 404));
        }

        var profile = await BuildAuthorProfileAsync(authorId, cancellationToken);
        return Ok(ApiResponse<AuthorProfileDto>.Success(profile, "更新成功"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpGet("me/softwares")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AuthorSoftwareDto>>>> ListSoftwaresAsync(CancellationToken cancellationToken)
    {
        var authorId = User.GetAuthorId();
        var softwares = await _authorService.GetSoftwaresAsync(authorId, cancellationToken);
        var items = softwares
            .Select(s => new AuthorSoftwareDto
            {
                SoftwareId = s.Id,
                DisplayName = s.DisplayName,
                ApiAddress = s.ApiAddress,
                ApiPort = s.ApiPort,
                SoftwareType = s.SoftwareType,
                SoftwareCode = s.SoftwareCode,
                CreatedAt = s.CreatedAt
            })
            .ToList();

        return Ok(ApiResponse<IReadOnlyCollection<AuthorSoftwareDto>>.Success(items));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpPost("me/softwares")]
    public async Task<ActionResult<ApiResponse<AuthorSoftwareDto>>> CreateSoftwareAsync([FromBody] AuthorSoftwareCreateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AuthorSoftwareDto>.Failure("参数错误"));
        }

        var authorId = User.GetAuthorId();
        var software = await _authorService.CreateSoftwareAsync(
            authorId,
            request.DisplayName.Trim(),
            request.ApiAddress.Trim(),
            request.ApiPort,
            request.SoftwareType.Trim().ToUpperInvariant(),
            cancellationToken);

        var dto = new AuthorSoftwareDto
        {
            SoftwareId = software.Id,
            DisplayName = software.DisplayName,
            ApiAddress = software.ApiAddress,
            ApiPort = software.ApiPort,
            SoftwareType = software.SoftwareType,
            SoftwareCode = software.SoftwareCode,
            CreatedAt = software.CreatedAt
        };

        return Ok(ApiResponse<AuthorSoftwareDto>.Success(dto, "新增成功"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpPut("me/softwares/{softwareId:int}")]
    public async Task<ActionResult<ApiResponse<AuthorSoftwareDto>>> UpdateSoftwareAsync(int softwareId, [FromBody] AuthorSoftwareUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<AuthorSoftwareDto>.Failure("参数错误"));
        }

        var authorId = User.GetAuthorId();
        var updated = await _authorService.UpdateSoftwareAsync(
            authorId,
            softwareId,
            request.DisplayName.Trim(),
            request.ApiAddress.Trim(),
            request.ApiPort,
            request.SoftwareType.Trim().ToUpperInvariant(),
            cancellationToken);

        if (updated is null)
        {
            return NotFound(ApiResponse<AuthorSoftwareDto>.Failure("软件不存在", 404));
        }

        var dto = new AuthorSoftwareDto
        {
            SoftwareId = updated.Id,
            DisplayName = updated.DisplayName,
            ApiAddress = updated.ApiAddress,
            ApiPort = updated.ApiPort,
            SoftwareType = updated.SoftwareType,
            SoftwareCode = updated.SoftwareCode,
            CreatedAt = updated.CreatedAt
        };

        return Ok(ApiResponse<AuthorSoftwareDto>.Success(dto, "更新成功"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpDelete("me/softwares/{softwareId:int}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteSoftwareAsync(int softwareId, CancellationToken cancellationToken)
    {
        var authorId = User.GetAuthorId();
        var deleted = await _authorService.DeleteSoftwareAsync(authorId, softwareId, cancellationToken);
        if (!deleted)
        {
            return BadRequest(ApiResponse<string>.Failure("无法删除最后一个软件码"));
        }

        return Ok(ApiResponse<string>.Success("ok", "已删除软件码"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpPost("me/regenerate-code")]
    public async Task<ActionResult<ApiResponse<AuthorSoftwareCodeResponse>>> RegenerateCodeAsync([FromQuery] int? softwareId, CancellationToken cancellationToken)
    {
        var authorId = User.GetAuthorId();
        var targetId = softwareId;

        if (targetId is null || targetId <= 0)
        {
            var softwares = await _authorService.GetSoftwaresAsync(authorId, cancellationToken);
            targetId = softwares.FirstOrDefault()?.Id;
        }

        if (targetId is null || targetId <= 0)
        {
            return NotFound(ApiResponse<AuthorSoftwareCodeResponse>.Failure("未找到可刷新软件码", 404));
        }

        var code = await _authorService.RegenerateSoftwareCodeAsync(authorId, targetId.Value, cancellationToken);
        return Ok(ApiResponse<AuthorSoftwareCodeResponse>.Success(new AuthorSoftwareCodeResponse(code), "已生成新的软件码"));
    }

    [Authorize(Roles = Roles.Author)]
    [HttpDelete("me")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteAsync(CancellationToken cancellationToken)
    {
        var authorId = User.GetAuthorId();
        var deleted = await _authorService.SoftDeleteAsync(authorId, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse<string>.Failure("账号不存在", 404));
        }

        return Ok(ApiResponse<string>.Success("ok", "账号已注销"));
    }

    private async Task<AuthorProfileDto> BuildAuthorProfileAsync(int authorId, CancellationToken cancellationToken)
    {
        var author = await _authorService.GetByIdAsync(authorId, cancellationToken);
        if (author is null)
        {
            throw new InvalidOperationException("账号不存在");
        }

        var softwares = await _authorService.GetSoftwaresAsync(authorId, cancellationToken);
        var softwareDtos = softwares
            .Select(s => new AuthorSoftwareDto
            {
                SoftwareId = s.Id,
                DisplayName = s.DisplayName,
                ApiAddress = s.ApiAddress,
                ApiPort = s.ApiPort,
                SoftwareType = s.SoftwareType,
                SoftwareCode = s.SoftwareCode,
                CreatedAt = s.CreatedAt
            })
            .ToList();

        var primary = softwareDtos.FirstOrDefault();

        return new AuthorProfileDto
        {
            Username = author.Username,
            Email = author.Email,
            DisplayName = primary?.DisplayName ?? author.DisplayName,
            ApiAddress = primary?.ApiAddress ?? author.ApiAddress,
            ApiPort = primary?.ApiPort ?? author.ApiPort,
            SoftwareType = primary?.SoftwareType ?? author.SoftwareType,
            SoftwareCode = primary?.SoftwareCode ?? author.SoftwareCode,
            Softwares = softwareDtos,
            PrimarySoftwareId = primary?.SoftwareId ?? (author.SoftwareId > 0 ? author.SoftwareId : null)
        };
    }
}
