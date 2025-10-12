using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Services;

public sealed class LanzouLinkService
{
    private static readonly Regex LinkRegex = new("链接[：:]+\\s*(?<url>https?://\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CodeRegex = new("提取码[：:]+\\s*(?<code>[A-Za-z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppConfig _config;
    private readonly ILogger<LanzouLinkService> _logger;

    public LanzouLinkService(AppConfig config, ILogger<LanzouLinkService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task<MySqlConnection?> TryOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = _config.Lanzou.BuildConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Lanzou database connection string is not configured");
            return null;
        }

        try
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Lanzou database connection");
            return null;
        }
    }

    public async Task<IReadOnlyList<LanzouLinkInfo>> GetAvailableLinksAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await TryOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return Array.Empty<LanzouLinkInfo>();
            }

            const string sql = "SELECT id, `链接` AS LinkContent, `创建时间` AS CreatedAt FROM lanzou_links";
            await using var command = new MySqlCommand(sql, connection);

            var rows = new List<LanzouLinkRow>();
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new LanzouLinkRow
                {
                    Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    LinkContent = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CreatedAt = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)
                };

                rows.Add(row);
            }

            var parsed = rows
                .Select(ParseRecord)
                .Where(info => info is not null)
                .Select(info => info!)
                .OrderByDescending(info => info.CreatedAt)
                .ThenByDescending(info => info.Id)
                .ToList();

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Lanzou links");
            return Array.Empty<LanzouLinkInfo>();
        }
    }

    public async Task<(IReadOnlyList<LanzouLinkRecord> Items, long Total)> QueryLinksAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await TryOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return (Array.Empty<LanzouLinkRecord>(), 0);
        }

        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);
        var offset = (safePage - 1) * safePageSize;

        var whereBuilder = new StringBuilder("WHERE 1=1");
        var parameterDefinitions = new List<ParameterDefinition>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            parameterDefinitions.Add(new ParameterDefinition(MySqlDbType.VarChar, $"%{keyword.Trim()}%", 512));
            whereBuilder.Append(" AND `链接` LIKE @p0");
        }

        var countSql = $"SELECT COUNT(*) FROM lanzou_links {whereBuilder}";
        long total;
        await using (var countCommand = new MySqlCommand(countSql, connection))
        {
            for (var i = 0; i < parameterDefinitions.Count; i++)
            {
                countCommand.Parameters.Add(parameterDefinitions[i].ToParameter(i));
            }

            var scalar = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            total = scalar switch
            {
                null or DBNull => 0,
                long l => l,
                int i => i,
                decimal dec => (long)dec,
                _ => Convert.ToInt64(scalar, CultureInfo.InvariantCulture)
            };
        }

        var limitIndex = parameterDefinitions.Count;
        var offsetIndex = parameterDefinitions.Count + 1;
        var dataSql = $"SELECT id, `链接` AS LinkContent, `创建时间` AS CreatedAt FROM lanzou_links {whereBuilder} ORDER BY id DESC LIMIT @p{limitIndex} OFFSET @p{offsetIndex}";

        var items = new List<LanzouLinkRecord>();
        await using (var dataCommand = new MySqlCommand(dataSql, connection))
        {
            for (var i = 0; i < parameterDefinitions.Count; i++)
            {
                dataCommand.Parameters.Add(parameterDefinitions[i].ToParameter(i));
            }

            dataCommand.Parameters.Add(new MySqlParameter($"@p{limitIndex}", MySqlDbType.Int32) { Value = safePageSize });
            dataCommand.Parameters.Add(new MySqlParameter($"@p{offsetIndex}", MySqlDbType.Int32) { Value = offset });

            await using var reader = await dataCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new LanzouLinkRow
                {
                    Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    LinkContent = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CreatedAt = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)
                };

                items.Add(ToRecord(row));
            }
        }

        return (items, total);
    }

    public async Task<int> DeleteLinksAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        if (ids is null)
        {
            return 0;
        }

        var distinctIds = ids
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return 0;
        }

        await using var connection = await TryOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return 0;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < distinctIds.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append($"@p{i}");
        }

        var sql = $"DELETE FROM lanzou_links WHERE id IN ({builder})";
        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < distinctIds.Count; i++)
        {
            command.Parameters.Add(new MySqlParameter($"@p{i}", MySqlDbType.Int64) { Value = distinctIds[i] });
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }

    private static LanzouLinkInfo? ParseRecord(LanzouLinkRow row)
    {
        if (string.IsNullOrWhiteSpace(row.LinkContent))
        {
            return null;
        }

        var urlMatch = LinkRegex.Match(row.LinkContent);
        var codeMatch = CodeRegex.Match(row.LinkContent);

        if (!urlMatch.Success || !codeMatch.Success)
        {
            return null;
        }

        var url = urlMatch.Groups["url"].Value.Trim();
        var code = codeMatch.Groups["code"].Value.Trim();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var createdAt = row.CreatedAt?.ToUniversalTime() ?? DateTime.UtcNow;

        return new LanzouLinkInfo
        {
            Id = row.Id,
            Url = url,
            ExtractionCode = code,
            CreatedAt = createdAt
        };
    }

    private static LanzouLinkRecord ToRecord(LanzouLinkRow row)
    {
        var parsed = ParseRecord(row);
        var createdAt = row.CreatedAt.HasValue
            ? DateTime.SpecifyKind(row.CreatedAt.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        return new LanzouLinkRecord
        {
            Id = row.Id,
            Url = parsed?.Url ?? string.Empty,
            ExtractionCode = parsed?.ExtractionCode ?? string.Empty,
            RawContent = row.LinkContent ?? string.Empty,
            CreatedAt = new DateTimeOffset(createdAt)
        };
    }

    private sealed class LanzouLinkRow
    {
        public long Id { get; set; }
        public string? LinkContent { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    private readonly struct ParameterDefinition
    {
        public ParameterDefinition(MySqlDbType type, object? value, int? size)
        {
            Type = type;
            Value = value;
            Size = size;
        }

        public MySqlDbType Type { get; }
        public object? Value { get; }
        public int? Size { get; }

        public MySqlParameter ToParameter(int index)
        {
            if (Size.HasValue)
            {
                return new MySqlParameter($"@p{index}", Type, Size.Value) { Value = Value ?? DBNull.Value };
            }

            return new MySqlParameter($"@p{index}", Type) { Value = Value ?? DBNull.Value };
        }
    }
}
