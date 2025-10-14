using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Services;

public interface IWeChatAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    Task<string> RefreshAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed class WeChatAccessTokenProvider : IWeChatAccessTokenProvider
{
    private const string CacheKey = "wechat:access_token";

    private readonly IMySqlConnectionFactory _connectionFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<WeChatAccessTokenProvider> _logger;
    private readonly WeChatOptions _options;

    public WeChatAccessTokenProvider(
        IMySqlConnectionFactory connectionFactory,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IOptions<WeChatOptions> options,
        ILogger<WeChatAccessTokenProvider> logger)
    {
        _connectionFactory = connectionFactory;
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppSecret))
        {
            throw new InvalidOperationException("请先在配置文件中设置 WeChat:AppId 和 WeChat:AppSecret。");
        }

        if (_memoryCache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var record = await GetStoredTokenAsync(cancellationToken);
        if (record != null && record.ExpiresAtUtc > DateTime.UtcNow.Add(_options.GetSafetyMargin()))
        {
            _memoryCache.Set(CacheKey, record.AccessToken, record.ExpiresAtUtc - DateTime.UtcNow);
            return record.AccessToken;
        }

        var token = await FetchTokenFromWeChatAsync(cancellationToken);
        return token;
    }

    public async Task<string> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        _memoryCache.Remove(CacheKey);
        return await FetchTokenFromWeChatAsync(cancellationToken);
    }

    private async Task<TokenRecord?> GetStoredTokenAsync(CancellationToken cancellationToken)
    {
        const string sql = @"SELECT AccessToken, ExpiresAtUtc FROM WeChatAccessTokens WHERE AppId = @AppId LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<TokenRecord>(
            new CommandDefinition(sql, new { AppId = _options.AppId }, cancellationToken: cancellationToken));
    }

    private async Task<string> FetchTokenFromWeChatAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(IWeChatAccessTokenProvider));
        var url = $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={_options.AppId}&secret={_options.AppSecret}";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("errcode", out var errorCodeElement) && errorCodeElement.GetInt32() != 0)
        {
            var errorCode = errorCodeElement.GetInt32();
            var errorMessage = document.RootElement.TryGetProperty("errmsg", out var errmsg) ? errmsg.GetString() : "";
            throw new InvalidOperationException($"获取微信 access_token 失败：{errorCode} {errorMessage}");
        }

        var accessToken = document.RootElement.GetProperty("access_token").GetString();
        var expiresIn = document.RootElement.GetProperty("expires_in").GetInt32();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("微信返回的 access_token 为空");
        }

        var safety = _options.GetSafetyMargin();
        var expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn)) - safety;

        await StoreTokenAsync(accessToken, expiresAt, cancellationToken);
        _memoryCache.Set(CacheKey, accessToken, expiresAt - DateTime.UtcNow);
        _logger.LogInformation("微信 access_token 已刷新，截止 {ExpiresAt}", expiresAt);
        return accessToken;
    }

    private async Task StoreTokenAsync(string token, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO WeChatAccessTokens (AppId, AccessToken, ExpiresAtUtc, UpdatedAtUtc)
VALUES (@AppId, @Token, @ExpiresAtUtc, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE AccessToken = VALUES(AccessToken), ExpiresAtUtc = VALUES(ExpiresAtUtc), UpdatedAtUtc = VALUES(UpdatedAtUtc);";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AppId = _options.AppId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        }, cancellationToken: cancellationToken));
    }

    private sealed record TokenRecord
    {
        public string AccessToken { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
    }
}
