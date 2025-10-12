using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SProtectAgentWeb.Api.Configuration;

namespace SProtectAgentWeb.Api.Services;

public sealed record CardDownloadHistoryEntry(long DownloadLinkId, string DownloadUrl, string? ExtractionCode, DateTime CreatedAt);

public sealed record CardVerificationLogEntry(
    long Id,
    string CardKey,
    string IpAddress,
    int AttemptNumber,
    bool WasSuccessful,
    long? DownloadLinkId,
    string? DownloadUrl,
    string? ExtractionCode,
    DateTime CreatedAt);

public sealed record CardVerificationSuccessSummary(
    string CardKey,
    long SuccessCount,
    DateTime FirstSuccessAt,
    DateTime LastSuccessAt);

public sealed class CardVerificationLogQuery
{
    public string? CardKey { get; init; }
    public string? IpAddress { get; init; }
    public string? Keyword { get; init; }
    public bool? WasSuccessful { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class CardVerificationRepository
{
    private readonly AppConfig _config;
    private readonly ILogger<CardVerificationRepository> _logger;
    private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
    private bool _schemaEnsured;
    private string? _connectionString;

    public CardVerificationRepository(AppConfig config, ILogger<CardVerificationRepository> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task<bool> EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured && !string.IsNullOrWhiteSpace(_connectionString))
        {
            return true;
        }

        await _schemaSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaEnsured && !string.IsNullOrWhiteSpace(_connectionString))
            {
                return true;
            }

            var connectionString = _config.Lanzou.BuildConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("Lanzou database connection string is not configured; verification attempts will not be persisted.");
                _connectionString = null;
                _schemaEnsured = false;
                return false;
            }

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            const string createTableSql = @"CREATE TABLE IF NOT EXISTS card_verification_log (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    card_key VARCHAR(128) NOT NULL,
    ip_address VARCHAR(64) NOT NULL,
    attempt_number INT NOT NULL,
    was_successful TINYINT(1) NOT NULL,
    download_link_id BIGINT NULL,
    download_url TEXT NULL,
    extraction_code VARCHAR(64) NULL,
    created_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    KEY idx_card_key (card_key),
    KEY idx_download_link_id (download_link_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using (var command = new MySqlCommand(createTableSql, connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _connectionString = connectionString;
            _schemaEnsured = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure card_verification_log schema");
            _connectionString = null;
            _schemaEnsured = false;
            return false;
        }
        finally
        {
            _schemaSemaphore.Release();
        }
    }

    private async Task<MySqlConnection?> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false) || string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public async Task<long> RecordAttemptAsync(
        string cardKey,
        string ipAddress,
        bool wasSuccessful,
        long? downloadLinkId,
        string? downloadUrl,
        string? extractionCode,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            _logger.LogWarning("Skipping verification attempt persistence because Lanzou database is unavailable.");
            return 1;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long attemptNumber;
        await using (var attemptCommand = new MySqlCommand(
                   "SELECT COALESCE(MAX(attempt_number), 0) + 1 FROM card_verification_log WHERE card_key = @CardKey FOR UPDATE",
                   connection,
                   transaction))
        {
            attemptCommand.Parameters.Add(CreateParameter("@CardKey", MySqlDbType.VarChar, cardKey, 128));
            var scalar = await attemptCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            attemptNumber = ConvertToInt64(scalar, 1);
            if (attemptNumber <= 0)
            {
                attemptNumber = 1;
            }
        }

        var createdAt = DateTime.UtcNow;
        await using (var insertCommand = new MySqlCommand(@"INSERT INTO card_verification_log
(card_key, ip_address, attempt_number, was_successful, download_link_id, download_url, extraction_code, created_at)
VALUES (@CardKey, @IpAddress, @AttemptNumber, @WasSuccessful, @DownloadLinkId, @DownloadUrl, @ExtractionCode, @CreatedAt);",
            connection,
            transaction))
        {
            insertCommand.Parameters.Add(CreateParameter("@CardKey", MySqlDbType.VarChar, cardKey, 128));
            insertCommand.Parameters.Add(CreateParameter("@IpAddress", MySqlDbType.VarChar, ipAddress, 64));
            insertCommand.Parameters.Add(CreateParameter("@AttemptNumber", MySqlDbType.Int32, attemptNumber));
            insertCommand.Parameters.Add(CreateParameter("@WasSuccessful", MySqlDbType.Int32, wasSuccessful ? 1 : 0));
            insertCommand.Parameters.Add(CreateParameter("@DownloadLinkId", MySqlDbType.Int64, downloadLinkId));
            insertCommand.Parameters.Add(CreateParameter("@DownloadUrl", MySqlDbType.Text, downloadUrl));
            insertCommand.Parameters.Add(CreateParameter("@ExtractionCode", MySqlDbType.VarChar, extractionCode, 64));
            insertCommand.Parameters.Add(CreateParameter("@CreatedAt", MySqlDbType.DateTime, createdAt));

            await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return attemptNumber;
    }

    public async Task<(IReadOnlyList<CardVerificationLogEntry> Items, long Total)> QueryLogsAsync(
        CardVerificationLogQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return (Array.Empty<CardVerificationLogEntry>(), 0);
        }

        var safePage = query.Page <= 0 ? 1 : query.Page;
        var safePageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 200);
        var offset = (safePage - 1) * safePageSize;

        var whereBuilder = new StringBuilder("WHERE 1=1");
        var parameters = new List<ParameterDefinition>();

        if (!string.IsNullOrWhiteSpace(query.CardKey))
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.VarChar, $"%{query.CardKey.Trim()}%", 128));
            whereBuilder.Append(" AND card_key LIKE @p").Append(parameters.Count - 1);
        }

        if (!string.IsNullOrWhiteSpace(query.IpAddress))
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.VarChar, $"%{query.IpAddress.Trim()}%", 64));
            whereBuilder.Append(" AND ip_address LIKE @p").Append(parameters.Count - 1);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.VarChar, $"%{query.Keyword.Trim()}%", 256));
            var index = parameters.Count - 1;
            whereBuilder.Append(" AND (download_url LIKE @p").Append(index).Append(" OR extraction_code LIKE @p").Append(index).Append(')');
        }

        if (query.WasSuccessful.HasValue)
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.Int32, query.WasSuccessful.Value ? 1 : 0, null));
            whereBuilder.Append(" AND was_successful = @p").Append(parameters.Count - 1);
        }

        if (query.StartTime.HasValue)
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.DateTime, query.StartTime.Value.UtcDateTime, null));
            whereBuilder.Append(" AND created_at >= @p").Append(parameters.Count - 1);
        }

        if (query.EndTime.HasValue)
        {
            parameters.Add(new ParameterDefinition(MySqlDbType.DateTime, query.EndTime.Value.UtcDateTime, null));
            whereBuilder.Append(" AND created_at <= @p").Append(parameters.Count - 1);
        }

        var countSql = $"SELECT COUNT(*) FROM card_verification_log {whereBuilder}";
        long total;
        await using (var countCommand = new MySqlCommand(countSql, connection))
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                countCommand.Parameters.Add(parameters[i].ToParameter(i));
            }

            var scalar = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            total = ConvertToInt64(scalar, 0);
        }

        var limitIndex = parameters.Count;
        var offsetIndex = parameters.Count + 1;
        var dataSql = $"SELECT id, card_key, ip_address, attempt_number, was_successful, download_link_id, download_url, extraction_code, created_at FROM card_verification_log {whereBuilder} ORDER BY id DESC LIMIT @p{limitIndex} OFFSET @p{offsetIndex}";

        var items = new List<CardVerificationLogEntry>();
        await using (var dataCommand = new MySqlCommand(dataSql, connection))
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                dataCommand.Parameters.Add(parameters[i].ToParameter(i));
            }

            dataCommand.Parameters.Add(CreateParameter($"@p{limitIndex}", MySqlDbType.Int32, safePageSize));
            dataCommand.Parameters.Add(CreateParameter($"@p{offsetIndex}", MySqlDbType.Int32, offset));

            await using var reader = await dataCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                var cardKeyValue = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var ipValue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var attemptNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                var wasSuccessful = !reader.IsDBNull(4) && reader.GetInt32(4) == 1;
                long? linkId = reader.IsDBNull(5) ? null : reader.GetInt64(5);
                string? url = reader.IsDBNull(6) ? null : reader.GetString(6);
                string? code = reader.IsDBNull(7) ? null : reader.GetString(7);
                var createdAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8);

                items.Add(new CardVerificationLogEntry(
                    id,
                    cardKeyValue,
                    ipValue,
                    attemptNumber,
                    wasSuccessful,
                    linkId,
                    url,
                    code,
                    DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)));
            }
        }

        return (items, total);
    }

    public async Task<int> DeleteLogsAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
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

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return 0;
        }

        var parameterNames = new List<string>();
        var builder = new StringBuilder();
        for (var i = 0; i < distinctIds.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var name = $"@p{i}";
            parameterNames.Add(name);
            builder.Append(name);
        }

        var sql = $"DELETE FROM card_verification_log WHERE id IN ({builder})";
        await using var command = new MySqlCommand(sql, connection);
        for (var i = 0; i < distinctIds.Count; i++)
        {
            command.Parameters.Add(CreateParameter(parameterNames[i], MySqlDbType.Int64, distinctIds[i]));
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }

    public async Task<IDictionary<long, int>> GetSuccessfulAssignmentsByLinkAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            _logger.LogWarning("Unable to query verification assignments because Lanzou database is unavailable.");
            return new Dictionary<long, int>();
        }

        const string sql = @"SELECT download_link_id, COUNT(*)
FROM card_verification_log
WHERE was_successful = 1 AND download_link_id IS NOT NULL
GROUP BY download_link_id;";

        await using var command = new MySqlCommand(sql, connection);
        var result = new Dictionary<long, int>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var linkId = reader.GetInt64(0);
            var count = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            result[linkId] = count;
        }

        return result;
    }

    public async Task<long?> GetLastSuccessfulLinkForCardAsync(string cardKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            _logger.LogWarning("Unable to look up previous link assignments because Lanzou database is unavailable.");
            return null;
        }

        const string sql = @"SELECT download_link_id
FROM card_verification_log
WHERE card_key = @CardKey AND was_successful = 1 AND download_link_id IS NOT NULL
ORDER BY id DESC
LIMIT 1;";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add(CreateParameter("@CardKey", MySqlDbType.VarChar, cardKey, 128));
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (scalar is null || scalar is DBNull)
        {
            return null;
        }

        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<CardDownloadHistoryEntry>> GetDownloadHistoryForCardAsync(string cardKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            _logger.LogWarning("Unable to load download history for {CardKey} because Lanzou database is unavailable.", cardKey);
            return Array.Empty<CardDownloadHistoryEntry>();
        }

        const string sql = @"SELECT download_link_id,
       download_url,
       extraction_code,
       created_at
FROM card_verification_log
WHERE card_key = @CardKey AND was_successful = 1 AND download_link_id IS NOT NULL
ORDER BY id ASC;";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add(CreateParameter("@CardKey", MySqlDbType.VarChar, cardKey, 128));

        var result = new List<CardDownloadHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var linkId = reader.GetInt64(0);
            var url = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var code = reader.IsDBNull(2) ? null : reader.GetString(2);
            var createdAt = reader.IsDBNull(3) ? DateTime.UtcNow : reader.GetDateTime(3);
            result.Add(new CardDownloadHistoryEntry(linkId, url, code, DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)));
        }

        return result;
    }

    public async Task<IReadOnlyList<CardVerificationSuccessSummary>> GetSuccessSummariesAsync(
        TimeSpan staleDuration,
        int minimumSuccessCount,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Array.Empty<CardVerificationSuccessSummary>();
        }

        var threshold = DateTime.UtcNow.Subtract(staleDuration);
        const string sql = @"SELECT card_key,
       COUNT(*) AS SuccessCount,
       MIN(created_at) AS FirstSuccessAt,
       MAX(created_at) AS LastSuccessAt
FROM card_verification_log
WHERE was_successful = 1
GROUP BY card_key
HAVING COUNT(*) >= @MinCount AND MAX(created_at) <= @Threshold";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add(CreateParameter("@MinCount", MySqlDbType.Int32, minimumSuccessCount));
        command.Parameters.Add(CreateParameter("@Threshold", MySqlDbType.DateTime, threshold));

        var result = new List<CardVerificationSuccessSummary>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var cardKey = reader.GetString(0);
            var count = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            var first = reader.IsDBNull(2) ? DateTime.UtcNow : reader.GetDateTime(2);
            var last = reader.IsDBNull(3) ? DateTime.UtcNow : reader.GetDateTime(3);

            result.Add(new CardVerificationSuccessSummary(
                cardKey,
                count,
                DateTime.SpecifyKind(first, DateTimeKind.Utc),
                DateTime.SpecifyKind(last, DateTimeKind.Utc)));
        }

        return result;
    }

    private static MySqlParameter CreateParameter(string name, MySqlDbType type, object? value, int? size = null)
    {
        var parameter = size.HasValue ? new MySqlParameter(name, type, size.Value) : new MySqlParameter(name, type);
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    private static long ConvertToInt64(object? value, long defaultValue)
    {
        return value switch
        {
            null or DBNull => defaultValue,
            long l => l,
            int i => i,
            decimal dec => (long)dec,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
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
            var name = $"@p{index}";
            return CreateParameter(name, Type, Value, Size);
        }
    }
}
