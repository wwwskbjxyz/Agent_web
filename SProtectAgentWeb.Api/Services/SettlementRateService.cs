using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Services;

public sealed class SettlementRateService
{
    private const string TableName = "card_settlement_rates";
    private const string AgentColumn = "agent_username";
    private const string IndexName = "ux_card_settlement_rates";
    private const string GlobalAgentKey = "__GLOBAL__";

    private readonly string _connectionString;
    private readonly ILogger<SettlementRateService> _logger;
    private volatile bool _schemaEnsured;

    public SettlementRateService(AppConfig config, ILogger<SettlementRateService> logger)
    {
        _connectionString = config?.Lanzou?.BuildConnectionString() ?? string.Empty;
        _logger = logger;
    }

    public Task<IReadOnlyList<SettlementRate>> GetRatesAsync(
        string software,
        string agentUsername,
        CancellationToken cancellationToken)
        => GetRatesAsync(software, agentUsername, null, cancellationToken);

    public async Task<IReadOnlyList<SettlementRate>> GetRatesAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedAgent = NormalizeAgent(agentUsername);
        if (string.IsNullOrEmpty(normalizedSoftware))
        {
            return Array.Empty<SettlementRate>();
        }

        string? normalizedFallback = null;
        if (!string.IsNullOrWhiteSpace(fallbackAgentUsername))
        {
            var fallbackCandidate = NormalizeAgent(fallbackAgentUsername);
            if (!string.Equals(fallbackCandidate, normalizedAgent, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fallbackCandidate, GlobalAgentKey, StringComparison.Ordinal))
            {
                normalizedFallback = fallbackCandidate;
            }
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Array.Empty<SettlementRate>();
        }

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SettlementRate> globalRates = Array.Empty<SettlementRate>();
        if (!string.Equals(normalizedAgent, GlobalAgentKey, StringComparison.Ordinal))
        {
            globalRates = await LoadRatesAsync(connection, normalizedSoftware, GlobalAgentKey, cancellationToken)
                .ConfigureAwait(false);
        }

        var agentRates = await LoadRatesAsync(connection, normalizedSoftware, normalizedAgent, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<SettlementRate> fallbackRates = Array.Empty<SettlementRate>();
        if (!string.IsNullOrEmpty(normalizedFallback))
        {
            fallbackRates = await GetRatesAsync(normalizedSoftware, normalizedFallback, cancellationToken)
                .ConfigureAwait(false);
        }

        var globalMap = globalRates.ToDictionary(rate => rate.CardType, StringComparer.OrdinalIgnoreCase);
        var agentMap = agentRates.ToDictionary(rate => rate.CardType, StringComparer.OrdinalIgnoreCase);
        var fallbackMap = fallbackRates.ToDictionary(rate => rate.CardType, StringComparer.OrdinalIgnoreCase);

        var cardTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in agentMap.Keys)
        {
            cardTypes.Add(key);
        }
        foreach (var key in fallbackMap.Keys)
        {
            cardTypes.Add(key);
        }
        foreach (var key in globalMap.Keys)
        {
            cardTypes.Add(key);
        }

        if (cardTypes.Count == 0)
        {
            return Array.Empty<SettlementRate>();
        }

        var ordered = cardTypes.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<SettlementRate>(ordered.Count);
        foreach (var cardType in ordered)
        {
            decimal price = 0m;
            if (agentMap.TryGetValue(cardType, out var agentRate))
            {
                price = agentRate.Price;
            }

            if (price <= 0 && fallbackMap.TryGetValue(cardType, out var fallbackRate) && fallbackRate.Price > 0)
            {
                price = fallbackRate.Price;
            }

            if (price <= 0 && globalMap.TryGetValue(cardType, out var globalRate) && globalRate.Price > 0)
            {
                price = globalRate.Price;
            }

            result.Add(new SettlementRate
            {
                AgentUsername = normalizedAgent,
                CardType = cardType,
                Price = price
            });
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetRateDictionaryAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
    {
        var rates = await GetRatesAsync(software, agentUsername, fallbackAgentUsername, cancellationToken)
            .ConfigureAwait(false);
        var dictionary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var rate in rates)
        {
            if (string.IsNullOrWhiteSpace(rate.CardType))
            {
                continue;
            }

            dictionary[rate.CardType.Trim()] = rate.Price;
        }

        return dictionary;
    }

    public async Task<IReadOnlyList<SettlementRate>> ReplaceRatesAsync(
        string software,
        string agentUsername,
        IReadOnlyCollection<SettlementRate> rates,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedAgent = NormalizeAgent(agentUsername);
        if (string.IsNullOrEmpty(normalizedSoftware))
        {
            return Array.Empty<SettlementRate>();
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return Array.Empty<SettlementRate>();
        }

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var normalizedRates = NormalizeRates(rates, normalizedAgent);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deleteSql = $"DELETE FROM `{TableName}` WHERE `software` = @Software AND `{AgentColumn}` = @Agent";
            await using (var delete = new MySqlCommand(deleteSql, connection, transaction))
            {
                delete.Parameters.AddWithValue("@Software", normalizedSoftware);
                delete.Parameters.AddWithValue("@Agent", normalizedAgent);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string insertSql = @"INSERT INTO card_settlement_rates(software, agent_username, card_type, price)
VALUES (@Software, @Agent, @CardType, @Price)";

            foreach (var rate in normalizedRates)
            {
                await using var insert = new MySqlCommand(insertSql, connection, transaction);
                insert.Parameters.AddWithValue("@Software", normalizedSoftware);
                insert.Parameters.AddWithValue("@Agent", normalizedAgent);
                insert.Parameters.AddWithValue("@CardType", rate.CardType);
                insert.Parameters.AddWithValue("@Price", rate.Price);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to persist settlement rates for {Software}/{Agent}", normalizedSoftware, normalizedAgent);
            throw;
        }

        return await GetRatesAsync(normalizedSoftware, normalizedAgent, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<SettlementRate>> LoadRatesAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        CancellationToken cancellationToken)
    {
        const string sql = @"SELECT card_type, price FROM card_settlement_rates
WHERE software = @Software AND agent_username = @Agent
ORDER BY card_type";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Software", software);
        command.Parameters.AddWithValue("@Agent", agentUsername);

        var results = new List<SettlementRate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var cardType = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var price = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
            if (string.IsNullOrWhiteSpace(cardType))
            {
                continue;
            }

            results.Add(new SettlementRate
            {
                AgentUsername = agentUsername,
                CardType = cardType.Trim(),
                Price = price
            });
        }

        return results;
    }

    private static string NormalizeSoftware(string software)
    {
        var trimmed = software?.Trim() ?? string.Empty;
        return trimmed.Length > 191 ? trimmed[..191] : trimmed;
    }

    private static string NormalizeAgent(string agentUsername)
    {
        var trimmed = agentUsername?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return GlobalAgentKey;
        }

        return trimmed.Length > 191 ? trimmed[..191] : trimmed;
    }

    private static IReadOnlyCollection<SettlementRate> NormalizeRates(
        IEnumerable<SettlementRate> rates,
        string agentUsername)
    {
        var dictionary = new Dictionary<string, SettlementRate>(StringComparer.OrdinalIgnoreCase);
        foreach (var rate in rates)
        {
            if (rate is null)
            {
                continue;
            }

            var cardType = rate.CardType?.Trim();
            if (string.IsNullOrWhiteSpace(cardType))
            {
                continue;
            }

            if (cardType.Length > 191)
            {
                cardType = cardType[..191];
            }

            var price = rate.Price < 0 ? 0 : Math.Round(rate.Price, 4, MidpointRounding.AwayFromZero);
            dictionary[cardType] = new SettlementRate
            {
                AgentUsername = agentUsername,
                CardType = cardType,
                Price = price
            };
        }

        return dictionary.Values;
    }

    private async Task<MySqlConnection?> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        try
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open settlement database connection");
            return null;
        }
    }

    private async Task EnsureSchemaAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        const string createTableSql = @"CREATE TABLE IF NOT EXISTS card_settlement_rates (
    id BIGINT NOT NULL AUTO_INCREMENT,
    software VARCHAR(191) NOT NULL,
    agent_username VARCHAR(191) NOT NULL DEFAULT '__GLOBAL__',
    card_type VARCHAR(191) NOT NULL,
    price DECIMAL(18,4) NOT NULL DEFAULT 0,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY ux_card_settlement_rates (software, agent_username, card_type)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";

        await using (var command = new MySqlCommand(createTableSql, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await EnsureAgentColumnAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureIndexAsync(connection, cancellationToken).ConfigureAwait(false);
        await NormalizeAgentValuesAsync(connection, cancellationToken).ConfigureAwait(false);

        _schemaEnsured = true;
    }

    private async Task EnsureAgentColumnAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, AgentColumn, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var alterSql = $"ALTER TABLE `{TableName}` ADD COLUMN `{AgentColumn}` VARCHAR(191) NOT NULL DEFAULT @Default AFTER `software`";
        await using var command = new MySqlCommand(alterSql, connection);
        command.Parameters.AddWithValue("@Default", GlobalAgentKey);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            _logger.LogWarning(ex, "Failed to add agent column to settlement rate table");
        }
    }

    private async Task EnsureIndexAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var desiredColumns = new[] { "software", AgentColumn, "card_type" };
        var matches = await IndexMatchesAsync(connection, IndexName, desiredColumns, cancellationToken)
            .ConfigureAwait(false);
        if (matches)
        {
            return;
        }

        var dropSql = $"ALTER TABLE `{TableName}` DROP INDEX `{IndexName}`";
        try
        {
            await using var drop = new MySqlCommand(dropSql, connection);
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            _logger.LogDebug(ex, "Unable to drop existing settlement index {Index}", IndexName);
        }

        var addSql = $"ALTER TABLE `{TableName}` ADD UNIQUE INDEX `{IndexName}` (`software`, `{AgentColumn}`, `card_type`)";
        try
        {
            await using var add = new MySqlCommand(addSql, connection);
            await add.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            _logger.LogWarning(ex, "Failed to ensure settlement index {Index}", IndexName);
        }
    }

    private async Task NormalizeAgentValuesAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"UPDATE `{TableName}` SET `{AgentColumn}` = @Default WHERE `{AgentColumn}` IS NULL OR `{AgentColumn}` = ''";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Default", GlobalAgentKey);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            _logger.LogDebug(ex, "Failed to normalize settlement agent usernames");
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        MySqlConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        const string sql = @"SELECT COUNT(1) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Table AND COLUMN_NAME = @Column";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Table", TableName);
        command.Parameters.AddWithValue("@Column", columnName);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result ?? 0) > 0;
    }

    private static async Task<bool> IndexMatchesAsync(
        MySqlConnection connection,
        string indexName,
        IReadOnlyList<string> expectedColumns,
        CancellationToken cancellationToken)
    {
        const string sql = @"SELECT COLUMN_NAME FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Table AND INDEX_NAME = @Index
ORDER BY SEQ_IN_INDEX";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Table", TableName);
        command.Parameters.AddWithValue("@Index", indexName);

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }

        if (columns.Count != expectedColumns.Count)
        {
            return false;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (!string.Equals(columns[i], expectedColumns[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
