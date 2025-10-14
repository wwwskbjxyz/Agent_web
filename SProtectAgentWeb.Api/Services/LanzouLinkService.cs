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
    private static readonly Regex SafeTableNameRegex = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private const string DefaultTableName = "lanzou_links";
    private static readonly int[] InitialAreaCodes =
    {
        45217, 45253, 45761, 46318, 46826, 47010, 47297, 47614,
        48119, 49062, 49324, 49896, 50371, 50614, 50622, 50906,
        51387, 51446, 52218, 52698, 52980, 53689, 54481
    };
    private static readonly char[] InitialLetters =
    {
        'a','b','c','d','e','f','g','h','j','k','l','m','n','o','p','q','r','s','t','w','x','y','z'
    };
    private static readonly Encoding Gb2312 = Encoding.GetEncoding("GB2312");

    private readonly AppConfig _config;
    private readonly ILogger<LanzouLinkService> _logger;

    static LanzouLinkService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

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

    public async Task<IReadOnlyList<LanzouLinkInfo>> GetAvailableLinksAsync(string software, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await TryOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return Array.Empty<LanzouLinkInfo>();
            }

            foreach (var table in EnumerateLinkTables(software))
            {
                try
                {
                    return await LoadAvailableLinksAsync(connection, table, cancellationToken).ConfigureAwait(false);
                }
                catch (MySqlException ex) when (ex.Number == (int)MySqlErrorCode.NoSuchTable)
                {
                    _logger.LogWarning(ex, "Link table {Table} not found; attempting fallback table.", table);
                }
            }

            return Array.Empty<LanzouLinkInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Lanzou links");
            return Array.Empty<LanzouLinkInfo>();
        }
    }

    public async Task<(IReadOnlyList<LanzouLinkRecord> Items, long Total)> QueryLinksAsync(
        string software,
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

        foreach (var table in EnumerateLinkTables(software))
        {
            try
            {
                return await QueryLinksInternal(connection, table, keyword, page, pageSize, cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException ex) when (ex.Number == (int)MySqlErrorCode.NoSuchTable)
            {
                _logger.LogWarning(ex, "Link table {Table} not found when paging links; attempting fallback.", table);
            }
        }

        return (Array.Empty<LanzouLinkRecord>(), 0);
    }

    public async Task<int> DeleteLinksAsync(string software, IEnumerable<long> ids, CancellationToken cancellationToken)
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

        foreach (var table in EnumerateLinkTables(software))
        {
            try
            {
                var affected = await DeleteLinksInternal(connection, table, distinctIds, cancellationToken).ConfigureAwait(false);
                if (affected > 0)
                {
                    return affected;
                }
            }
            catch (MySqlException ex) when (ex.Number == (int)MySqlErrorCode.NoSuchTable)
            {
                _logger.LogWarning(ex, "Link table {Table} not found when deleting records; attempting fallback.", table);
            }
        }

        return 0;
    }

    private async Task<IReadOnlyList<LanzouLinkInfo>> LoadAvailableLinksAsync(MySqlConnection connection, string table, CancellationToken cancellationToken)
    {
        table = EnsureSafeTableName(table);
        var sql = $"SELECT id, `链接` AS LinkContent, `创建时间` AS CreatedAt FROM {table}";
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

        return rows
            .Select(ParseRecord)
            .Where(info => info is not null)
            .Select(info => info!)
            .OrderByDescending(info => info.CreatedAt)
            .ThenByDescending(info => info.Id)
            .ToList();
    }

    private async Task<(IReadOnlyList<LanzouLinkRecord> Items, long Total)> QueryLinksInternal(
        MySqlConnection connection,
        string table,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        table = EnsureSafeTableName(table);

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

        var countSql = $"SELECT COUNT(*) FROM {table} {whereBuilder}";
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
        var dataSql = $"SELECT id, `链接` AS LinkContent, `创建时间` AS CreatedAt FROM {table} {whereBuilder} ORDER BY id DESC LIMIT @p{limitIndex} OFFSET @p{offsetIndex}";

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

    private async Task<int> DeleteLinksInternal(MySqlConnection connection, string table, IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        table = EnsureSafeTableName(table);
        var builder = new StringBuilder();
        for (var i = 0; i < ids.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append($"@p{i}");
        }

        var sql = $"DELETE FROM {table} WHERE id IN ({builder})";
        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < ids.Count; i++)
        {
            command.Parameters.Add(new MySqlParameter($"@p{i}", MySqlDbType.Int64) { Value = ids[i] });
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<string> EnumerateLinkTables(string software)
    {
        var resolved = ResolveTableName(software);
        if (!string.Equals(resolved, DefaultTableName, StringComparison.OrdinalIgnoreCase))
        {
            yield return resolved;
        }

        yield return DefaultTableName;
    }

    private static string ResolveTableName(string software)
    {
        var slug = GenerateSlug(software);
        if (string.IsNullOrEmpty(slug) || string.Equals(slug, DefaultTableName, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultTableName;
        }

        var candidate = $"{DefaultTableName}_{slug}";
        return SafeTableNameRegex.IsMatch(candidate) ? candidate : DefaultTableName;
    }

    private static string GenerateSlug(string software)
    {
        if (string.IsNullOrWhiteSpace(software))
        {
            return string.Empty;
        }

        var normalized = software.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder();
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                builder.Append(rune.ToString().ToLowerInvariant());
            }
            else if (char.IsWhiteSpace((char)rune.Value) || rune.Value is '-' or '_')
            {
                builder.Append('_');
            }
            else if (IsChineseRune(rune))
            {
                builder.Append(GetChineseInitial(rune));
            }
            else
            {
                builder.Append('_');
            }
        }

        var collapsed = Regex.Replace(builder.ToString(), "_{2,}", "_");
        var trimmed = collapsed.Trim('_');
        return trimmed.ToLowerInvariant();
    }

    private static bool IsChineseRune(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x4E00 and <= 0x9FFF;
    }

    private static string GetChineseInitial(Rune rune)
    {
        if (!rune.IsBmp)
        {
            return $"u{rune.Value:x4}";
        }

        try
        {
            var bytes = Gb2312.GetBytes(new[] { (char)rune.Value });
            if (bytes.Length < 2)
            {
                return rune.ToString().ToLowerInvariant();
            }

            var code = (bytes[0] << 8) + bytes[1];
            for (var i = 0; i < InitialAreaCodes.Length; i++)
            {
                var max = i == InitialAreaCodes.Length - 1 ? 55290 : InitialAreaCodes[i + 1];
                if (code >= InitialAreaCodes[i] && code < max)
                {
                    return InitialLetters[i].ToString();
                }
            }
        }
        catch
        {
            // Ignore encoding failures and fall back below.
        }

        return rune.ToString().ToLowerInvariant();
    }

    private static string EnsureSafeTableName(string table)
    {
        if (!SafeTableNameRegex.IsMatch(table))
        {
            throw new InvalidOperationException($"Unsafe table name detected: {table}");
        }

        return table;
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
