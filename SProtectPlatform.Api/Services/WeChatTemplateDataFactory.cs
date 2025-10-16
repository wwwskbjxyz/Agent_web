using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Controllers;
using SProtectPlatform.Api.Models.Dto;

namespace SProtectPlatform.Api.Services;

public interface IWeChatTemplateDataFactory
{
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string templateKey,
        string userType,
        int userId,
        CancellationToken cancellationToken = default);
}

public sealed class WeChatTemplateDataFactory : IWeChatTemplateDataFactory
{
    private readonly IAgentService _agentService;
    private readonly IAuthorService _authorService;
    private readonly IBindingService _bindingService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeChatTemplateDataFactory> _logger;
    private readonly ConcurrentDictionary<string, PermissionCacheEntry> _permissionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WeChatTemplateDataFactory(
        IAgentService agentService,
        IAuthorService authorService,
        IBindingService bindingService,
        ICredentialProtector credentialProtector,
        IHttpClientFactory httpClientFactory,
        ILogger<WeChatTemplateDataFactory> logger)
    {
        _agentService = agentService;
        _authorService = authorService;
        _bindingService = bindingService;
        _credentialProtector = credentialProtector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string templateKey,
        string userType,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            switch (templateKey)
            {
                case WeChatTemplateKeys.InstantCommunication:
                    await PopulateInstantCommunicationAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                case WeChatTemplateKeys.BlacklistAlert:
                    await PopulateBlacklistAlertAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                case WeChatTemplateKeys.SettlementNotice:
                    await PopulateSettlementNoticeAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成模板 {TemplateKey} 的动态数据时发生异常", templateKey);
        }

        return data;
    }

    private async Task PopulateInstantCommunicationAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? subjectName = null;
        string? systemName = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var agent = await _agentService.GetAuthRecordByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (agent != null)
            {
                subjectName = Prefer(agent.DisplayName, agent.Username, agent.Email);
            }

            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            if (binding != null)
            {
                systemName = Prefer(binding.AuthorDisplayName, binding.SoftwareCode);
            }
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (author != null)
            {
                subjectName = Prefer(author.DisplayName, author.Email, author.Username);
                systemName = Prefer(author.DisplayName, author.SoftwareCode);
            }
        }

        if (!string.IsNullOrWhiteSpace(subjectName))
        {
            data.TryAdd("thing1", $"{subjectName} 有新的即时沟通消息");
        }

        data.TryAdd("phrase2", "待处理");

        var now = FormatDateTime(DateTimeOffset.UtcNow);
        data.TryAdd("time4", now);
        data.TryAdd("time5", now);

        if (string.IsNullOrWhiteSpace(systemName))
        {
            systemName = subjectName;
        }

        if (!string.IsNullOrWhiteSpace(systemName))
        {
            data.TryAdd("thing6", systemName);
        }
    }

    private async Task PopulateBlacklistAlertAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? softwareName = null;
        bool? hasPermission = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            softwareName = Prefer(binding?.AuthorDisplayName, binding?.SoftwareCode);
            if (binding != null)
            {
                hasPermission = await DetermineBlacklistPermissionAsync(binding, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            softwareName = Prefer(author?.DisplayName, author?.SoftwareCode);
            hasPermission = true;
        }

        var now = FormatDateTime(DateTimeOffset.UtcNow);
        data.TryAdd("time3", now);

        if (!string.IsNullOrWhiteSpace(softwareName))
        {
            data.TryAdd("thing5", $"软件 {softwareName} 检测到新的黑名单异常，请尽快核查处理");
        }

        if (hasPermission.HasValue)
        {
            data["hasBlacklistPermission"] = hasPermission.Value ? "true" : "false";
        }
    }

    private async Task PopulateSettlementNoticeAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? softwareCode = null;
        string? softwareName = null;
        string? agentLabel = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var agent = await _agentService.GetAuthRecordByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            agentLabel = Prefer(agent?.DisplayName, agent?.Username, agent?.Email);

            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            if (binding != null)
            {
                softwareCode = binding.SoftwareCode;
                softwareName = Prefer(binding.AuthorDisplayName, binding.SoftwareType);
            }
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (author != null)
            {
                softwareCode = author.SoftwareCode;
                softwareName = Prefer(author.DisplayName, author.SoftwareType);
            }
        }

        if (!string.IsNullOrWhiteSpace(softwareCode))
        {
            data.TryAdd("character_string1", softwareCode);
        }

        data.TryAdd("time3", FormatDate(DateTimeOffset.UtcNow));

        var summary = !string.IsNullOrWhiteSpace(softwareName)
            ? $"{softwareName} 待结算，请及时处理"
            : null;

        if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrWhiteSpace(agentLabel))
        {
            summary = $"账户 {agentLabel} 有新的结算提醒";
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            data.TryAdd("thing5", summary);
        }

        if (!data.ContainsKey("number6"))
        {
            data.TryAdd("number6", "1");
        }
    }

    private async Task<bool> DetermineBlacklistPermissionAsync(BindingRecord binding, CancellationToken cancellationToken)
    {
        var cacheKey = BuildPermissionCacheKey(binding);

        if (_permissionCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return cached.IsSuper;
            }

            _permissionCache.TryRemove(cacheKey, out _);
        }

        var remoteResult = await CheckAuthorSuperAsync(binding, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var ttl = remoteResult.HasValue
            ? (remoteResult.Value ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3))
            : TimeSpan.FromSeconds(30);

        var entry = new PermissionCacheEntry(remoteResult ?? false, now.Add(ttl));
        _permissionCache[cacheKey] = entry;

        return entry.IsSuper;
    }

    private async Task<bool?> CheckAuthorSuperAsync(BindingRecord binding, CancellationToken cancellationToken)
    {
        if (binding is null)
        {
            return null;
        }

        var username = binding.AuthorAccount?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        string password;
        try
        {
            password = _credentialProtector.Unprotect(binding.EncryptedAuthorPassword);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to decrypt author password for binding {BindingId} (software {SoftwareCode})",
                binding.BindingId,
                binding.SoftwareCode);
            return null;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var host = binding.ApiAddress?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var uriBuilder = new UriBuilder
        {
            Scheme = Uri.UriSchemeHttp,
            Host = host,
            Port = binding.ApiPort > 0 ? binding.ApiPort : -1,
            Path = "api/Auth/login"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri)
        {
            Content = JsonContent.Create(new RemoteLoginRequest(username, password), options: JsonOptions)
        };

        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SProtectPlatform", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = _httpClientFactory.CreateClient(nameof(WeChatTemplateDataFactory));

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Blacklist permission probe failed with status {Status} for binding {BindingId} (software {SoftwareCode})",
                    (int)response.StatusCode,
                    binding.BindingId,
                    binding.SoftwareCode);
                return false;
            }

            RemoteApiResponse<RemoteLoginResponse>? envelope;
            try
            {
                envelope = await response.Content
                    .ReadFromJsonAsync<RemoteApiResponse<RemoteLoginResponse>>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse blacklist permission response for binding {BindingId} (software {SoftwareCode})",
                    binding.BindingId,
                    binding.SoftwareCode);
                return null;
            }

            if (envelope is null)
            {
                return null;
            }

            if (envelope.Code != 0)
            {
                _logger.LogWarning(
                    "Remote login rejected with code {Code} for binding {BindingId} (software {SoftwareCode})",
                    envelope.Code,
                    binding.BindingId,
                    binding.SoftwareCode);
                return false;
            }

            return envelope.Data?.IsSuper;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Exception occurred while probing blacklist permission for binding {BindingId} (software {SoftwareCode})",
                binding.BindingId,
                binding.SoftwareCode);
            return null;
        }
    }

    private static string BuildPermissionCacheKey(BindingRecord binding)
    {
        var account = binding.AuthorAccount?.Trim() ?? string.Empty;
        var password = binding.EncryptedAuthorPassword ?? string.Empty;
        var software = binding.SoftwareCode?.Trim() ?? string.Empty;
        return string.Join("|", binding.BindingId, account, password, software);
    }

    private async Task<BindingRecord?> GetPrimaryBindingForAgentAsync(int agentId, CancellationToken cancellationToken)
    {
        var bindings = await _bindingService.GetBindingsForAgentAsync(agentId, cancellationToken).ConfigureAwait(false);
        return bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.SoftwareCode))
            .OrderByDescending(binding => binding.BindingId)
            .FirstOrDefault();
    }

    private static string? Prefer(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatDateTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record PermissionCacheEntry(bool IsSuper, DateTimeOffset ExpiresAtUtc);

    private sealed record RemoteLoginRequest(string Username, string Password);

    private sealed class RemoteLoginResponse
    {
        public bool IsSuper { get; set; }
    }

    private sealed class RemoteApiResponse<T>
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
