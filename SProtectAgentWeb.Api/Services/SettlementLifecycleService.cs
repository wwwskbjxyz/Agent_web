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

public sealed class SettlementLifecycleService
{
    private const string ProfileTable = "card_settlement_profiles";
    private const string BillsTable = "card_settlement_bills";
    private const string GlobalAgentKey = "__GLOBAL__";
    private const int MinutesPerDay = 24 * 60;

    private readonly string _connectionString;
    private readonly ILogger<SettlementLifecycleService> _logger;
    private volatile bool _schemaEnsured;

    public SettlementLifecycleService(AppConfig config, ILogger<SettlementLifecycleService> logger)
    {
        _connectionString = config?.Lanzou?.BuildConnectionString() ?? string.Empty;
        _logger = logger;
    }

    public Task<(SettlementCycleInfo Cycle, IReadOnlyList<SettlementBill> Bills)> GetDetailsAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
        => GetDetailsInternalAsync(software, agentUsername, fallbackAgentUsername, ensurePending: true, cancellationToken);

    public Task<SettlementCycleInfo> GetCycleAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
        => GetCycleInternalAsync(software, agentUsername, fallbackAgentUsername, cancellationToken);

    public async Task<SettlementCycleInfo> UpdateCycleAsync(
        string software,
        string agentUsername,
        int? cycleDays,
        int? cycleTimeMinutes,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedAgent = NormalizeAgent(agentUsername);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return new SettlementCycleInfo { AgentUsername = normalizedAgent };
        }

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var profile = await GetOrCreateProfileAsync(connection, normalizedSoftware, normalizedAgent, cancellationToken)
            .ConfigureAwait(false);

        if (cycleDays.HasValue || cycleTimeMinutes.HasValue)
        {
            var sanitizedDays = cycleDays.HasValue && cycleDays.Value > 0 ? cycleDays.Value : (cycleDays.HasValue ? 0 : profile.CycleDays);
            var sanitizedMinutes = cycleTimeMinutes.HasValue
                ? NormalizeCycleTime(cycleTimeMinutes.Value)
                : profile.CycleTimeMinutes;

            DateTime? lastSettled = profile.LastSettledAtUtc;
            DateTime? nextDue = profile.NextDueAtUtc;

            if (sanitizedDays > 0)
            {
                var anchor = AlignToPreviousBoundary(DateTime.UtcNow, sanitizedMinutes);
                lastSettled = anchor;
                nextDue = anchor.AddDays(sanitizedDays);
            }
            else
            {
                nextDue = null;
                if (lastSettled is null)
                {
                    lastSettled = DateTime.UtcNow;
                }
            }

            const string updateSql =
                @"UPDATE card_settlement_profiles
SET cycle_days = @Cycle,
    cycle_time_minutes = @CycleTime,
    last_settled_at = @LastSettled,
    next_due_at = @NextDue,
    updated_at = UTC_TIMESTAMP()
WHERE software = @Software AND agent_username = @Agent";

            await using var command = new MySqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@Cycle", sanitizedDays);
            command.Parameters.AddWithValue("@CycleTime", sanitizedMinutes);
            command.Parameters.AddWithValue("@LastSettled", lastSettled ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@NextDue", nextDue ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Software", normalizedSoftware);
            command.Parameters.AddWithValue("@Agent", normalizedAgent);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return await BuildCycleInfoAsync(connection, normalizedSoftware, normalizedAgent, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CompleteBillAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        long billId,
        decimal amount,
        string? note,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedAgent = NormalizeAgent(agentUsername);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return;
        }

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string selectSql =
                @"SELECT id, cycle_start, cycle_end, status FROM card_settlement_bills
WHERE id = @Id AND software = @Software AND agent_username = @Agent
FOR UPDATE";

            SettlementBill? bill = null;
            await using (var select = new MySqlCommand(selectSql, connection, transaction))
            {
                select.Parameters.AddWithValue("@Id", billId);
                select.Parameters.AddWithValue("@Software", normalizedSoftware);
                select.Parameters.AddWithValue("@Agent", normalizedAgent);

                await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    bill = new SettlementBill
                    {
                        Id = reader.GetInt64(0),
                        Software = normalizedSoftware,
                        AgentUsername = normalizedAgent,
                        CycleStartUtc = reader.GetDateTime(1),
                        CycleEndUtc = reader.GetDateTime(2),
                        Status = reader.GetInt32(3)
                    };
                }
            }

            if (bill is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (bill.Status > 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var cycleInfo = await BuildCycleInfoAsync(
                    connection,
                    normalizedSoftware,
                    normalizedAgent,
                    fallbackAgentUsername,
                    cancellationToken)
                .ConfigureAwait(false);

            const string updateSql =
                @"UPDATE card_settlement_bills
SET amount = @Amount,
    note = @Note,
    status = 1,
    settled_at = UTC_TIMESTAMP()
WHERE id = @Id";

            await using (var update = new MySqlCommand(updateSql, connection, transaction))
            {
                update.Parameters.AddWithValue("@Amount", Math.Round(amount < 0 ? 0 : amount, 2, MidpointRounding.AwayFromZero));
                update.Parameters.AddWithValue("@Note", string.IsNullOrWhiteSpace(note) ? (object)DBNull.Value : note.Trim());
                update.Parameters.AddWithValue("@Id", bill.Id);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var nextDue = cycleInfo.EffectiveCycleDays > 0
                ? bill.CycleEndUtc.AddDays(cycleInfo.EffectiveCycleDays)
                : (DateTime?)null;

            const string profileSql =
                @"UPDATE card_settlement_profiles
SET last_settled_at = @Settled,
    next_due_at = @Next,
    updated_at = UTC_TIMESTAMP()
WHERE software = @Software AND agent_username = @Agent";

            await using (var profileUpdate = new MySqlCommand(profileSql, connection, transaction))
            {
                profileUpdate.Parameters.AddWithValue("@Settled", bill.CycleEndUtc);
                profileUpdate.Parameters.AddWithValue("@Next", nextDue ?? (object)DBNull.Value);
                profileUpdate.Parameters.AddWithValue("@Software", normalizedSoftware);
                profileUpdate.Parameters.AddWithValue("@Agent", normalizedAgent);

                await profileUpdate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to complete settlement bill {Bill} for {Software}/{Agent}", billId, normalizedSoftware, normalizedAgent);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetReminderMapAsync(
        string software,
        IEnumerable<string> agentUsernames,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedFallback = string.IsNullOrWhiteSpace(fallbackAgentUsername)
            ? null
            : NormalizeAgent(fallbackAgentUsername);

        var agents = agentUsernames
            .Select(NormalizeAgent)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (agents.Length == 0)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            var cycle = await GetCycleInternalAsync(normalizedSoftware, agent, normalizedFallback, cancellationToken)
                .ConfigureAwait(false);
            result[agent] = cycle.IsDue;
        }

        return result;
    }

    private async Task<(SettlementCycleInfo Cycle, IReadOnlyList<SettlementBill> Bills)> GetDetailsInternalAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        bool ensurePending,
        CancellationToken cancellationToken)
    {
        var normalizedSoftware = NormalizeSoftware(software);
        var normalizedAgent = NormalizeAgent(agentUsername);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return (new SettlementCycleInfo { AgentUsername = normalizedAgent }, Array.Empty<SettlementBill>());
        }

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var cycle = await BuildCycleInfoAsync(connection, normalizedSoftware, normalizedAgent, fallbackAgentUsername, cancellationToken)
            .ConfigureAwait(false);

        if (ensurePending && cycle.IsDue)
        {
            var profile = await GetOrCreateProfileAsync(connection, normalizedSoftware, normalizedAgent, cancellationToken)
                .ConfigureAwait(false);
            await EnsurePendingBillAsync(connection, profile, cycle.EffectiveCycleDays, cancellationToken)
                .ConfigureAwait(false);
        }

        var bills = await LoadBillsAsync(connection, normalizedSoftware, normalizedAgent, cancellationToken)
            .ConfigureAwait(false);

        return (cycle, bills);
    }

    private Task<SettlementCycleInfo> GetCycleInternalAsync(
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
        => GetDetailsInternalAsync(software, agentUsername, fallbackAgentUsername, ensurePending: false, cancellationToken)
            .ContinueWith(task => task.Result.Cycle, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    private async Task<SettlementCycleInfo> BuildCycleInfoAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        string? fallbackAgentUsername,
        CancellationToken cancellationToken)
    {
        var profiles = new List<SettlementProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var target = await GetOrCreateProfileAsync(connection, software, agentUsername, cancellationToken)
            .ConfigureAwait(false);
        profiles.Add(target);
        seen.Add(agentUsername);

        if (!string.IsNullOrWhiteSpace(fallbackAgentUsername))
        {
            var fallback = NormalizeAgent(fallbackAgentUsername);
            if (seen.Add(fallback))
            {
                var fallbackProfile = await GetOrCreateProfileAsync(connection, software, fallback, cancellationToken)
                    .ConfigureAwait(false);
                profiles.Add(fallbackProfile);
            }
        }

        if (seen.Add(GlobalAgentKey))
        {
            var globalProfile = await GetOrCreateProfileAsync(connection, software, GlobalAgentKey, cancellationToken)
                .ConfigureAwait(false);
            profiles.Add(globalProfile);
        }

        target.CycleTimeMinutes = NormalizeCycleTime(target.CycleTimeMinutes);

        SettlementProfile source = target;
        foreach (var candidate in profiles)
        {
            if (candidate.CycleDays > 0)
            {
                candidate.CycleTimeMinutes = NormalizeCycleTime(candidate.CycleTimeMinutes);
                source = candidate;
                break;
            }
        }

        var effective = source.CycleDays > 0 ? source.CycleDays : 0;
        var effectiveTimeMinutes = source.CycleTimeMinutes;
        var inherited = !string.Equals(source.AgentUsername, target.AgentUsername, StringComparison.OrdinalIgnoreCase)
                         && effective > 0;

        if (target.LastSettledAtUtc is null)
        {
            DateTime? alignedLast = null;
            if (effective > 0)
            {
                alignedLast = AlignToPreviousBoundary(DateTime.UtcNow, effectiveTimeMinutes);
            }

            await UpdateProfileTimesAsync(
                    connection,
                    software,
                    target.AgentUsername,
                    alignedLast ?? DateTime.UtcNow,
                    target.NextDueAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            target = await GetOrCreateProfileAsync(connection, software, target.AgentUsername, cancellationToken)
                .ConfigureAwait(false);
            target.CycleTimeMinutes = NormalizeCycleTime(target.CycleTimeMinutes);
        }

        if (effective > 0 && target.NextDueAtUtc is null)
        {
            var baseTime = target.LastSettledAtUtc ?? AlignToPreviousBoundary(DateTime.UtcNow, effectiveTimeMinutes);
            var nextDue = baseTime.AddDays(effective);
            await UpdateProfileTimesAsync(
                    connection,
                    software,
                    target.AgentUsername,
                    baseTime,
                    nextDue,
                    cancellationToken)
                .ConfigureAwait(false);
            target = await GetOrCreateProfileAsync(connection, software, target.AgentUsername, cancellationToken)
                .ConfigureAwait(false);
            target.CycleTimeMinutes = NormalizeCycleTime(target.CycleTimeMinutes);
        }

        return new SettlementCycleInfo
        {
            AgentUsername = target.AgentUsername,
            OwnCycleDays = target.CycleDays,
            EffectiveCycleDays = effective,
            OwnCycleTimeMinutes = target.CycleTimeMinutes,
            EffectiveCycleTimeMinutes = effectiveTimeMinutes,
            IsInherited = inherited,
            LastSettledAtUtc = target.LastSettledAtUtc,
            NextDueAtUtc = target.NextDueAtUtc
        };
    }

    private async Task UpdateProfileTimesAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        DateTime lastSettled,
        DateTime? nextDue,
        CancellationToken cancellationToken)
    {
        const string sql =
            @"UPDATE card_settlement_profiles
SET last_settled_at = @LastSettled,
    next_due_at = @NextDue,
    updated_at = UTC_TIMESTAMP()
WHERE software = @Software AND agent_username = @Agent";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LastSettled", lastSettled);
        command.Parameters.AddWithValue("@NextDue", nextDue ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Software", software);
        command.Parameters.AddWithValue("@Agent", agentUsername);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsurePendingBillAsync(
        MySqlConnection connection,
        SettlementProfile profile,
        int effectiveCycleDays,
        CancellationToken cancellationToken)
    {
        if (effectiveCycleDays <= 0)
        {
            return;
        }

        if (!profile.NextDueAtUtc.HasValue || profile.NextDueAtUtc.Value > DateTime.UtcNow)
        {
            return;
        }

        const string sql =
            @"SELECT id FROM card_settlement_bills
WHERE software = @Software AND agent_username = @Agent AND status = 0
LIMIT 1";

        await using (var command = new MySqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@Software", profile.Software);
            command.Parameters.AddWithValue("@Agent", profile.AgentUsername);

            var existing = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                return;
            }
        }

        var cycleEnd = profile.NextDueAtUtc.Value;
        var cycleStart = profile.LastSettledAtUtc ?? cycleEnd.AddDays(-effectiveCycleDays);

        const string insertSql =
            @"INSERT INTO card_settlement_bills
(software, agent_username, cycle_start, cycle_end, amount, status, created_at)
VALUES (@Software, @Agent, @Start, @End, 0, 0, UTC_TIMESTAMP())";

        await using var insert = new MySqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("@Software", profile.Software);
        insert.Parameters.AddWithValue("@Agent", profile.AgentUsername);
        insert.Parameters.AddWithValue("@Start", cycleStart);
        insert.Parameters.AddWithValue("@End", cycleEnd);

        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SettlementBill>> LoadBillsAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        CancellationToken cancellationToken)
    {
        const string sql =
            @"SELECT id, cycle_start, cycle_end, amount, status, note, created_at, settled_at
FROM card_settlement_bills
WHERE software = @Software AND agent_username = @Agent
ORDER BY status ASC, cycle_end DESC
LIMIT 50";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Software", software);
        command.Parameters.AddWithValue("@Agent", agentUsername);

        var list = new List<SettlementBill>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var bill = new SettlementBill
            {
                Id = reader.GetInt64(0),
                Software = software,
                AgentUsername = agentUsername,
                CycleStartUtc = reader.GetDateTime(1),
                CycleEndUtc = reader.GetDateTime(2),
                Amount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                Status = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Note = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAtUtc = reader.GetDateTime(6),
                SettledAtUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            };
            list.Add(bill);
        }

        return list;
    }

    private async Task<SettlementProfile> GetOrCreateProfileAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        CancellationToken cancellationToken)
    {
        var existing = await LoadProfileAsync(connection, software, agentUsername, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        const string insertSql =
            @"INSERT INTO card_settlement_profiles
(software, agent_username, cycle_days, cycle_time_minutes, last_settled_at, next_due_at, created_at, updated_at)
VALUES (@Software, @Agent, 0, 0, NULL, NULL, UTC_TIMESTAMP(), UTC_TIMESTAMP())";

        await using (var insert = new MySqlCommand(insertSql, connection))
        {
            insert.Parameters.AddWithValue("@Software", software);
            insert.Parameters.AddWithValue("@Agent", agentUsername);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return await LoadProfileAsync(connection, software, agentUsername, cancellationToken)
            .ConfigureAwait(false) ?? new SettlementProfile
        {
            Software = software,
            AgentUsername = agentUsername,
            CycleDays = 0,
            CycleTimeMinutes = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static async Task<SettlementProfile?> LoadProfileAsync(
        MySqlConnection connection,
        string software,
        string agentUsername,
        CancellationToken cancellationToken)
    {
        const string sql =
            @"SELECT id, cycle_days, cycle_time_minutes, last_settled_at, next_due_at, created_at, updated_at
FROM card_settlement_profiles
WHERE software = @Software AND agent_username = @Agent
LIMIT 1";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Software", software);
        command.Parameters.AddWithValue("@Agent", agentUsername);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SettlementProfile
        {
            Id = reader.GetInt64(0),
            Software = software,
            AgentUsername = agentUsername,
            CycleDays = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            CycleTimeMinutes = NormalizeCycleTime(reader.IsDBNull(2) ? 0 : reader.GetInt32(2)),
            LastSettledAtUtc = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            NextDueAtUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            CreatedAtUtc = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
            UpdatedAtUtc = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6)
        };
    }

    private async Task EnsureSchemaAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        const string profileSql =
            @"CREATE TABLE IF NOT EXISTS card_settlement_profiles (
    id BIGINT NOT NULL AUTO_INCREMENT,
    software VARCHAR(191) NOT NULL,
    agent_username VARCHAR(191) NOT NULL,
    cycle_days INT NOT NULL DEFAULT 0,
    cycle_time_minutes INT NOT NULL DEFAULT 0,
    last_settled_at DATETIME NULL,
    next_due_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY ux_card_settlement_profiles (software, agent_username)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";

        await using (var command = new MySqlCommand(profileSql, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string ensureTimeColumn =
            @"SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'card_settlement_profiles' AND COLUMN_NAME = 'cycle_time_minutes'
LIMIT 1";

        await using (var check = new MySqlCommand(ensureTimeColumn, connection))
        {
            var exists = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (exists == null)
            {
                const string alterSql =
                    "ALTER TABLE card_settlement_profiles ADD COLUMN cycle_time_minutes INT NOT NULL DEFAULT 0 AFTER cycle_days";
                await using var alter = new MySqlCommand(alterSql, connection);
                await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        const string billSql =
            @"CREATE TABLE IF NOT EXISTS card_settlement_bills (
    id BIGINT NOT NULL AUTO_INCREMENT,
    software VARCHAR(191) NOT NULL,
    agent_username VARCHAR(191) NOT NULL,
    cycle_start DATETIME NOT NULL,
    cycle_end DATETIME NOT NULL,
    amount DECIMAL(18,4) NOT NULL DEFAULT 0,
    status TINYINT NOT NULL DEFAULT 0,
    note VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    settled_at DATETIME NULL,
    PRIMARY KEY (id),
    INDEX ix_card_settlement_bills_agent (software, agent_username, status)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";

        await using (var command = new MySqlCommand(billSql, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _schemaEnsured = true;
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
            _logger.LogError(ex, "Failed to open settlement lifecycle connection");
            return null;
        }
    }

    private static int NormalizeCycleTime(int minutes)
    {
        if (minutes <= 0)
        {
            return 0;
        }

        var normalized = minutes % MinutesPerDay;
        if (normalized < 0)
        {
            normalized += MinutesPerDay;
        }

        return normalized;
    }

    private static DateTime AlignToPreviousBoundary(DateTime referenceUtc, int cycleMinutes)
    {
        var boundary = BuildBoundary(referenceUtc, cycleMinutes);
        if (boundary > referenceUtc)
        {
            boundary = boundary.AddDays(-1);
        }

        return boundary;
    }

    private static DateTime BuildBoundary(DateTime referenceUtc, int cycleMinutes)
    {
        var normalized = NormalizeCycleTime(cycleMinutes);
        var baseDate = new DateTime(referenceUtc.Year, referenceUtc.Month, referenceUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        return baseDate.AddMinutes(normalized);
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
}
