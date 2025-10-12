using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public sealed class IpLocationCacheRepository
{
    private readonly AppConfig _config;
    private readonly ILogger<IpLocationCacheRepository> _logger;

    public IpLocationCacheRepository(AppConfig config, ILogger<IpLocationCacheRepository> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    private string GetDatabasePath()
    {
        var dataPath = _config.GetDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new InvalidOperationException("未配置数据目录，无法初始化 IP 归属地缓存库");
        }

        var directory = Path.Combine(dataPath, "map_ip");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "ip_location_cache.db");
    }

    public async Task<IReadOnlyDictionary<string, IpLocationCacheEntry>> GetAsync(IEnumerable<string> ips, CancellationToken cancellationToken)
    {
        var ipList = (ips ?? Array.Empty<string>())
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Select(ip => ip.Trim())
            .Where(ip => ip.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ipList.Count == 0)
        {
            return new Dictionary<string, IpLocationCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dbPath = GetDatabasePath();
            cancellationToken.ThrowIfCancellationRequested();

            var rows = await Task.Run(
                    () => SqliteBridge.GetIpLocations(dbPath, ipList),
                    cancellationToken)
                .ConfigureAwait(false);

            return rows
                .Select(row => new IpLocationCacheEntry
                {
                    Ip = row.Ip,
                    Province = row.Province,
                    City = row.City,
                    District = row.District,
                    UpdatedAt = row.UpdatedAt
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.Ip))
                .Select(Normalize)
                .ToDictionary(row => row.Ip, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 IP 归属地缓存失败");
            return new Dictionary<string, IpLocationCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task UpsertAsync(IEnumerable<IpLocationCacheEntry> records, CancellationToken cancellationToken)
    {
        var list = (records ?? Array.Empty<IpLocationCacheEntry>())
            .Where(record => record != null && !string.IsNullOrWhiteSpace(record.Ip))
            .Select(Normalize)
            .ToList();

        if (list.Count == 0)
        {
            return;
        }

        try
        {
            var dbPath = GetDatabasePath();
            cancellationToken.ThrowIfCancellationRequested();

            var payloads = list
                .Select(record => new SqliteBridge.IpLocationRecord(
                    record.Ip,
                    record.Province,
                    record.City,
                    record.District,
                    record.UpdatedAt))
                .ToList();

            await Task.Run(() => SqliteBridge.UpsertIpLocations(dbPath, payloads), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入 IP 归属地缓存失败");
        }
    }

    private static IpLocationCacheEntry Normalize(IpLocationCacheEntry entry)
    {
        entry.Ip = entry.Ip?.Trim() ?? string.Empty;
        entry.Province = entry.Province?.Trim() ?? string.Empty;
        entry.City = entry.City?.Trim() ?? string.Empty;
        entry.District = entry.District?.Trim() ?? string.Empty;
        return entry;
    }
}

public sealed class IpLocationCacheEntry
{
    public string Ip { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public long UpdatedAt { get; set; }
}
