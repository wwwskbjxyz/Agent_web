using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public sealed class UsageDistributionBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly AppConfig _config;
    private readonly ILogger<UsageDistributionBackgroundService> _logger;

    public UsageDistributionBackgroundService(IServiceProvider serviceProvider, AppConfig config, ILogger<UsageDistributionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Usage distribution cache refresh failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var dataPath = _config.GetDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            _logger.LogWarning("数据目录未配置，无法刷新地区分布缓存");
            return;
        }

        var mapDirectory = Path.Combine(dataPath, "map_ip");
        Directory.CreateDirectory(mapDirectory);

        using var scope = _serviceProvider.CreateScope();
        var databaseManager = scope.ServiceProvider.GetRequiredService<DatabaseManager>();
        var cardService = scope.ServiceProvider.GetRequiredService<CardService>();
        var cacheRepository = scope.ServiceProvider.GetRequiredService<UsageDistributionCacheRepository>();

        var softwareSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var softwares = await databaseManager.GetEnabledSoftwaresAsync().ConfigureAwait(false);
            foreach (var name in softwares)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    softwareSet.Add(name.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取软件列表失败，将根据数据库文件尝试刷新地区分布缓存");
        }

        foreach (var file in Directory.EnumerateFiles(dataPath, "idc*.db", SearchOption.TopDirectoryOnly))
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                continue;
            }

            if (string.Equals(fileNameWithoutExtension, "idc", StringComparison.OrdinalIgnoreCase))
            {
                softwareSet.Add("默认软件");
            }
            else if (fileNameWithoutExtension.StartsWith("idc_", StringComparison.OrdinalIgnoreCase))
            {
                softwareSet.Add(fileNameWithoutExtension.Substring(4));
            }
        }

        foreach (var software in softwareSet)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshotFileName = string.Equals(software, "默认软件", StringComparison.OrdinalIgnoreCase)
                ? "idc.db"
                : $"idc_{software}.db";
            var sourcePath = Path.Combine(dataPath, snapshotFileName);
            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("软件 {Software} 的数据库文件不存在: {Path}", software, sourcePath);
                continue;
            }

            string databasePath;
            try
            {
                databasePath = await databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "准备软件 {Software} 数据库失败", software);
                continue;
            }

            try
            {
                await RefreshSoftwareCacheAsync(software, databasePath, cardService, cacheRepository, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "直接读取数据库 {Software} 失败，将尝试备份后读取", software);
            }

            var snapshotPath = Path.Combine(mapDirectory, snapshotFileName);
            try
            {
                await BackupDatabaseAsync(sourcePath, snapshotPath, cancellationToken).ConfigureAwait(false);
                await RefreshSoftwareCacheAsync(software, snapshotPath, cardService, cacheRepository, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "备份数据库 {Software} 失败", software);
            }
        }
    }

    private static async Task BackupDatabaseAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };

        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        await using var source = new SqliteConnection(sourceBuilder.ToString());
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var destination = new SqliteConnection(destinationBuilder.ToString());
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

        source.BackupDatabase(destination);
    }

    private static async Task RefreshSoftwareCacheAsync(string software, string databasePath, CardService cardService, UsageDistributionCacheRepository repository, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var entries = new List<UsageDistributionCacheEntry>();

        var overall = await cardService
            .ComputeUsageDistributionAsync(databasePath, creators: null, cancellationToken, software, allowBackgroundRefresh: false)
            .ConfigureAwait(false);
        entries.Add(new UsageDistributionCacheEntry
        {
            Software = software,
            Whom = UsageDistributionCacheRepository.AllKey,
            Response = overall,
            UpdatedAt = now
        });

        IReadOnlyList<string> creators;
        try
        {
            creators = await Task.Run(() => SqliteBridge.GetCardCreators(databasePath)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取软件 {software} 的制卡人失败", ex);
        }

        foreach (var creator in creators.Select(c => c?.Trim()).Where(c => !string.IsNullOrEmpty(c)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await cardService
                .ComputeUsageDistributionAsync(databasePath, new[] { creator! }, cancellationToken, software, allowBackgroundRefresh: false)
                .ConfigureAwait(false);
            entries.Add(new UsageDistributionCacheEntry
            {
                Software = software,
                Whom = creator!,
                Response = response,
                UpdatedAt = now
            });
        }

        await repository.ReplaceEntriesAsync(software, entries, cancellationToken).ConfigureAwait(false);
    }
}
