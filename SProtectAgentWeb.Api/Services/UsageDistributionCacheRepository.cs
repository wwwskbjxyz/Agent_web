using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public sealed class UsageDistributionCacheRepository
{
    public const string AllKey = "__ALL__";

    private readonly AppConfig _config;
    private readonly ILogger<UsageDistributionCacheRepository> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    public UsageDistributionCacheRepository(AppConfig config, ILogger<UsageDistributionCacheRepository> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private string GetDatabasePath()
    {
        var dataPath = _config.GetDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new InvalidOperationException("未配置数据库目录");
        }

        var directory = Path.Combine(dataPath, "map_ip");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "usage_distribution_cache.db");
    }

    public async Task ReplaceEntriesAsync(string software, IEnumerable<UsageDistributionCacheEntry> entries, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            throw new ArgumentException("software 不能为空", nameof(software));
        }

        var records = (entries ?? Array.Empty<UsageDistributionCacheEntry>())
            .Where(e => e != null)
            .Select(e => new
            {
                Software = software,
                Whom = NormalizeKey(e.Whom),
                Payload = JsonSerializer.Serialize(e.Response ?? new UsageDistributionResponse(), _serializerOptions),
                ResolvedTotal = e.Response?.ResolvedTotal ?? 0,
                UpdatedAt = e.UpdatedAt
            })
            .ToList();

        var payloads = records
            .Select(r => new SqliteBridge.UsageDistributionRecord(r.Whom, r.Payload, r.ResolvedTotal, r.UpdatedAt))
            .ToList();

        var dbPath = GetDatabasePath();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await Task.Run(() => SqliteBridge.ReplaceUsageDistributionEntries(dbPath, software, payloads), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入 UsageDistribution 缓存失败");
            throw;
        }
    }

    public async Task<IList<UsageDistributionCacheEntry>> GetEntriesAsync(string software, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return Array.Empty<UsageDistributionCacheEntry>();
        }

        var normalizedKeys = (keys ?? Array.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(NormalizeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedKeys.Count == 0)
        {
            return Array.Empty<UsageDistributionCacheEntry>();
        }

        var dbPath = GetDatabasePath();
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<SqliteBridge.UsageDistributionRecord> rows;
        try
        {
            rows = await Task.Run(
                    () => SqliteBridge.GetUsageDistributionEntries(dbPath, software, normalizedKeys),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 UsageDistribution 缓存失败");
            return Array.Empty<UsageDistributionCacheEntry>();
        }

        var list = new List<UsageDistributionCacheEntry>();
        foreach (var row in rows)
        {
            try
            {
                var response = JsonSerializer.Deserialize<UsageDistributionResponse>(row.Payload, _serializerOptions)
                               ?? new UsageDistributionResponse();
                response.ResolvedTotal = row.ResolvedTotal;
                list.Add(new UsageDistributionCacheEntry
                {
                    Software = software,
                    Whom = row.Whom,
                    Response = response,
                    UpdatedAt = row.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析 UsageDistribution 缓存失败: 软件={Software}, Whom={Whom}", software, row.Whom);
            }
        }

        return list;
    }

    private static string NormalizeKey(string? key)
    {
        var trimmed = (key ?? string.Empty).Trim();
        return string.IsNullOrEmpty(trimmed) ? AllKey : trimmed;
    }
}

public sealed class UsageDistributionCacheEntry
{
    public string Software { get; set; } = string.Empty;
    public string Whom { get; set; } = string.Empty;
    public UsageDistributionResponse? Response { get; set; }
    public long UpdatedAt { get; set; }
}
