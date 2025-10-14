using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Data;

namespace SProtectPlatform.Api.Services;

public interface ICorsOriginService
{
    Task<IReadOnlyCollection<string>> GetAllowedOriginsAsync(CancellationToken cancellationToken = default);
}

public sealed class CorsOriginService : ICorsOriginService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly IMySqlConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CorsOriginService> _logger;

    public CorsOriginService(IMySqlConnectionFactory connectionFactory, IMemoryCache cache, ILogger<CorsOriginService> logger)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> GetAllowedOriginsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyCollection<string>>("cors:origins", out var cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        const string sql = "SELECT Origin FROM AllowedOrigins";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var origins = (await connection.QueryAsync<string>(sql)).Select(origin => origin.Trim()).Where(origin => !string.IsNullOrEmpty(origin)).ToList();

        EnsureOrigin(origins, "http://localhost");
        EnsureOrigin(origins, "https://localhost");
        EnsureOrigin(origins, "http://127.0.0.1");
        EnsureOrigin(origins, "https://127.0.0.1");
        EnsureOrigin(origins, "http://localhost:8080");
        EnsureOrigin(origins, "https://localhost:8080");
        EnsureOrigin(origins, "http://127.0.0.1:8080");
        EnsureOrigin(origins, "https://127.0.0.1:8080");

        _cache.Set("cors:origins", origins, CacheDuration);
        _logger.LogDebug("Loaded {Count} allowed origins from database", origins.Count);
        return origins;
    }

    private static void EnsureOrigin(ICollection<string> collection, string origin)
    {
        if (!collection.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            collection.Add(origin);
        }
    }
}
