using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Models.Dto;

namespace SProtectPlatform.Api.Services;

public interface ICardVerificationForwarder
{
    Task<ApiResponse<CardVerificationResultDto>> VerifyAsync(
        CardVerificationRequestDto request,
        CancellationToken cancellationToken = default);
}

public sealed class CardVerificationForwarder : ICardVerificationForwarder
{
    private readonly IBindingService _bindingService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CardVerificationForwarder> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CardVerificationForwarder(
        IBindingService bindingService,
        ICredentialProtector credentialProtector,
        IHttpClientFactory httpClientFactory,
        ILogger<CardVerificationForwarder> logger)
    {
        _bindingService = bindingService;
        _credentialProtector = credentialProtector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ApiResponse<CardVerificationResultDto>> VerifyAsync(
        CardVerificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var trimmedCode = request.SoftwareCode?.Trim();
        var trimmedSoftware = request.Software?.Trim();
        var trimmedCardKey = request.CardKey?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedCode) ||
            string.IsNullOrWhiteSpace(trimmedSoftware) ||
            string.IsNullOrWhiteSpace(trimmedCardKey))
        {
            return ApiResponse<CardVerificationResultDto>.Failure("参数不完整", 400);
        }

        var binding = await _bindingService.GetBindingBySoftwareCodeAsync(trimmedCode, cancellationToken);
        if (binding is null)
        {
            return ApiResponse<CardVerificationResultDto>.Failure("未绑定该软件码", 404);
        }

        string authorPassword;
        try
        {
            authorPassword = _credentialProtector.Unprotect(binding.EncryptedAuthorPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt author password for software {SoftwareCode}", trimmedCode);
            return ApiResponse<CardVerificationResultDto>.Failure("验证失败，请稍后再试", 500);
        }

        var remoteUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeHttp,
            Host = binding.ApiAddress,
            Port = binding.ApiPort,
            Path = "api/CardVerification/verify"
        }.Uri;

        trimmedSoftware = string.IsNullOrWhiteSpace(trimmedSoftware)
            ? (binding.SoftwareType?.Trim() ?? binding.SoftwareCode?.Trim() ?? trimmedCode)
            : trimmedSoftware;

        var payload = new
        {
            cardKey = trimmedCardKey,
            software = trimmedSoftware,
            softwareCode = binding.SoftwareCode?.Trim() ?? trimmedCode,
            agentAccount = string.IsNullOrWhiteSpace(request.AgentAccount)
                ? null
                : request.AgentAccount!.Trim()
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, remoteUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };

        message.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse("SProtectPlatform/1.0"));
        message.Headers.TryAddWithoutValidation("X-SProtect-Author-Account", binding.AuthorAccount);
        message.Headers.TryAddWithoutValidation("X-SProtect-Author-Password", authorPassword);

        var client = _httpClientFactory.CreateClient(nameof(CardVerificationForwarder));
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Forward card verification failed with status {Status} for software {SoftwareCode}",
                    (int)response.StatusCode,
                    trimmedCode);
                return ApiResponse<CardVerificationResultDto>.Failure("验证失败，请稍后再试", (int)response.StatusCode);
            }

            ApiResponse<CardVerificationResultDto>? forwarded;
            try
            {
                forwarded = JsonSerializer.Deserialize<ApiResponse<CardVerificationResultDto>>(responseText, SerializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse verification response for software {SoftwareCode}: {Payload}", trimmedCode, responseText);
                forwarded = null;
            }

            if (forwarded is null)
            {
                return ApiResponse<CardVerificationResultDto>.Failure("验证失败，请稍后再试", 500);
            }

            return forwarded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Card verification forwarding threw for software {SoftwareCode}", trimmedCode);
            return ApiResponse<CardVerificationResultDto>.Failure("验证失败，请稍后再试", 500);
        }
    }
}
