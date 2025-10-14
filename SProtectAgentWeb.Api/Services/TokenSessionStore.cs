using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Services;

public class TokenSessionStore
{
    private const string CacheKeyPrefix = "token_session:";
    private readonly IMemoryCache _cache;
    private readonly ILogger<TokenSessionStore> _logger;

    public TokenSessionStore(IMemoryCache cache, ILogger<TokenSessionStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public void Set(string tokenId, UserSession session, DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        ArgumentNullException.ThrowIfNull(session);

        var key = GetCacheKey(tokenId);
        var entry = new TokenSessionEntry(session, expiresAtUtc);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAtUtc
        };

        _cache.Set(key, entry, options);
        _logger.LogDebug("Session cached for token {TokenId} until {Expires}", tokenId, expiresAtUtc);
    }

    public UserSession? Get(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        return _cache.TryGetValue(GetCacheKey(tokenId), out TokenSessionEntry? entry) ? entry.Session : null;
    }

    public DateTimeOffset? GetExpiration(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        return _cache.TryGetValue(GetCacheKey(tokenId), out TokenSessionEntry? entry) ? entry.ExpiresAtUtc : null;
    }

    public bool Exists(string tokenId) => !string.IsNullOrWhiteSpace(tokenId) && _cache.TryGetValue(GetCacheKey(tokenId), out _);

    public void Remove(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return;
        }

        _cache.Remove(GetCacheKey(tokenId));
        _logger.LogDebug("Session removed for token {TokenId}", tokenId);
    }

    private static string GetCacheKey(string tokenId) => CacheKeyPrefix + tokenId;

    private sealed record TokenSessionEntry(UserSession Session, DateTimeOffset ExpiresAtUtc);
}
