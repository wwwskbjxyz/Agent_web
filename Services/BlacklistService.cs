using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public class BlacklistService
{
    private readonly DatabaseManager _databaseManager;
    private readonly ILogger<BlacklistService> _logger;

    public BlacklistService(DatabaseManager databaseManager, ILogger<BlacklistService> logger)
    {
        _databaseManager = databaseManager;
        _logger = logger;
    }

    public async Task<IList<BlacklistEntry>> GetMachineListAsync(IEnumerable<string>? softwares, int? type)
    {
        var aggregated = new List<BlacklistMachineRow>();
        var targets = await ResolveSoftwareTargetsAsync(softwares).ConfigureAwait(false);

        foreach (var software in targets)
        {
            string dbPath;
            try
            {
                dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "准备软件 {Software} 的数据库失败", software);
                continue;
            }

            IReadOnlyList<SqliteBridge.BlacklistMachineRecord> rows;
            try
            {
                rows = await Task.Run(() => SqliteBridge.GetBlacklistMachines(dbPath, type))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取软件 {Software} 的黑名单失败", software);
                continue;
            }

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Value))
                {
                    continue;
                }

                var entry = new BlacklistEntry
                {
                    Value = row.Value,
                    Type = row.Type,
                    Remarks = row.Remarks,
                    Software = software
                };

                aggregated.Add(new BlacklistMachineRow(row.RowId, entry));
            }
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        return aggregated
            .OrderBy(row => IsDefaultSoftware(row.Entry.Software) ? 0 : 1)
            .ThenBy(row => row.Entry.Software, comparer)
            .ThenByDescending(row => row.RowId)
            .Select(row => row.Entry)
            .ToList();
    }

    public async Task AddMachineAsync(string value, int type, string? remarks)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("机器码不能为空", nameof(value));
        }

        var normalizedValue = value.Trim();
        var normalizedRemarks = remarks?.Trim() ?? string.Empty;

        string dbPath;
        try
        {
            dbPath = await _databaseManager.PrepareDatabasePathAsync("默认软件").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认软件数据库失败");
            throw;
        }

        await Task.Run(() => SqliteBridge.AddBlacklistMachine(dbPath, normalizedValue, type, normalizedRemarks)).ConfigureAwait(false);
    }

    public async Task<IList<BlacklistLogEntry>> GetLogsAsync(IEnumerable<string>? softwares, int limit)
    {
        var resolvedLimit = Math.Clamp(limit <= 0 ? 200 : limit, 1, 1000);
        var aggregated = new List<BlacklistLogRow>();
        var targets = await ResolveSoftwareTargetsAsync(softwares).ConfigureAwait(false);

        foreach (var software in targets)
        {
            string dbPath;
            try
            {
                dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "准备软件 {Software} 的数据库失败", software);
                continue;
            }

            IReadOnlyList<SqliteBridge.BlacklistLogRecord> rows;
            try
            {
                rows = await Task.Run(() => SqliteBridge.GetBlacklistLogs(dbPath, resolvedLimit))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取软件 {Software} 的黑名单日志失败", software);
                continue;
            }

            foreach (var row in rows)
            {
                var entry = new BlacklistLogEntry
                {
                    ID = row.Id,
                    IP = row.Ip,
                    Card = row.Card,
                    PCSign = row.PcSign,
                    Timestamp = row.Timestamp,
                    ErrEvents = row.ErrEvents,
                    Software = software
                };

                aggregated.Add(new BlacklistLogRow(row.RowId, entry));
            }
        }

        return aggregated
            .OrderByDescending(row => row.Entry.Timestamp)
            .ThenByDescending(row => row.RowId)
            .ThenByDescending(row => row.Entry.ID)
            .Take(resolvedLimit)
            .Select(row => row.Entry)
            .ToList();
    }

    public async Task DeleteMachinesAsync(IEnumerable<string> values)
    {
        var targets = (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        string dbPath;
        try
        {
            dbPath = await _databaseManager.PrepareDatabasePathAsync("默认软件").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化默认软件数据库失败");
            throw;
        }

        await Task.Run(() => SqliteBridge.DeleteBlacklistMachines(dbPath, targets)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> ResolveSoftwareTargetsAsync(IEnumerable<string>? softwares)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var set = new HashSet<string>(comparer);

        if (softwares != null)
        {
            foreach (var software in softwares)
            {
                if (string.IsNullOrWhiteSpace(software))
                {
                    continue;
                }

                set.Add(software.Trim());
            }
        }

        if (set.Count == 0)
        {
            try
            {
                var enabled = await _databaseManager.GetEnabledSoftwaresAsync().ConfigureAwait(false);
                foreach (var software in enabled)
                {
                    if (string.IsNullOrWhiteSpace(software))
                    {
                        continue;
                    }

                    set.Add(software.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取启用的软件列表失败");
            }
        }

        set.Add("默认软件");
        return set.ToList();
    }

    private static bool IsDefaultSoftware(string software)
    {
        return string.Equals(software, "默认软件", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct BlacklistMachineRow(long RowId, BlacklistEntry Entry);

    private readonly record struct BlacklistLogRow(long RowId, BlacklistLogEntry Entry);
}

