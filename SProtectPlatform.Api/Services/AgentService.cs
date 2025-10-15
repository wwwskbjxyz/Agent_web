using System;
using Dapper;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Models;

namespace SProtectPlatform.Api.Services;

public interface IAgentService
{
    Task<Agent?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Agent?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<AgentAuthRecord?> GetAuthRecordByAccountAsync(string account, CancellationToken cancellationToken = default);
    Task<AgentAuthRecord?> GetAuthRecordByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Agent?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Agent> CreateAsync(string username, string email, string passwordHash, string displayName, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class AgentService : IAgentService
{
    private readonly IMySqlConnectionFactory _connectionFactory;

    public AgentService(IMySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string AgentSelectColumns = "CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, DisplayName, COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) AS CreatedAt";

    public async Task<Agent?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AgentSelectColumns} FROM Agents WHERE LOWER(Email) = @Email LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Agent>(sql, new { Email = email.ToLowerInvariant() });
    }

    public async Task<Agent?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AgentSelectColumns} FROM Agents WHERE LOWER(Username) = @Username LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await IdentityHelper.ColumnExistsAsync(connection, "Agents", "Username", cancellationToken))
        {
            return null;
        }
        return await connection.QuerySingleOrDefaultAsync<Agent>(sql, new { Username = username.ToLowerInvariant() });
    }

    public async Task<AgentAuthRecord?> GetAuthRecordByAccountAsync(string account, CancellationToken cancellationToken = default)
    {
        var emailSql = $"SELECT CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, PasswordHash, DisplayName FROM Agents WHERE LOWER(Email) = @Account LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var normalized = account.ToLowerInvariant();
        var record = await connection.QuerySingleOrDefaultAsync<AgentAuthRecord>(emailSql, new { Account = normalized });

        if (record != null)
        {
            return record;
        }

        if (!await IdentityHelper.ColumnExistsAsync(connection, "Agents", "Username", cancellationToken))
        {
            return null;
        }

        var usernameSql = $"SELECT CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, PasswordHash, DisplayName FROM Agents WHERE LOWER(Username) = @Account LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<AgentAuthRecord>(usernameSql, new { Account = normalized });
    }

    public async Task<AgentAuthRecord?> GetAuthRecordByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, PasswordHash, DisplayName FROM Agents WHERE Id = @Id LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AgentAuthRecord>(sql, new { Id = id });
    }

    public async Task<Agent?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {AgentSelectColumns} FROM Agents WHERE Id = @Id LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Agent>(sql, new { Id = id });
    }

    public async Task<Agent> CreateAsync(string username, string email, string passwordHash, string displayName, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var hasUsernameColumn = await IdentityHelper.ColumnExistsAsync(connection, "Agents", "Username", cancellationToken);
        var hasCreatedAtColumn = await IdentityHelper.ColumnExistsAsync(connection, "Agents", "CreatedAt", cancellationToken);
        var hasCreatedAtUtcColumn = await IdentityHelper.ColumnExistsAsync(connection, "Agents", "CreatedAtUtc", cancellationToken);

        var baseColumns = "Email, PasswordHash, DisplayName";
        var baseValues = "@Email, @PasswordHash, @DisplayName";

        if (hasUsernameColumn)
        {
            baseColumns = "Username, " + baseColumns;
            baseValues = "@Username, " + baseValues;
        }

        if (hasCreatedAtColumn)
        {
            baseColumns += ", CreatedAt";
            baseValues += ", @CreatedAt";
        }

        if (hasCreatedAtUtcColumn)
        {
            baseColumns += ", CreatedAtUtc";
            baseValues += ", @CreatedAtUtc";
        }

        var now = DateTime.UtcNow;

        var parameters = new
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            CreatedAt = now,
            CreatedAtUtc = now
        };

        if (await IdentityHelper.ColumnHasAutoIncrementAsync(connection, "Agents", cancellationToken))
        {
            var autoSql = $@"INSERT INTO Agents ({baseColumns})
VALUES ({baseValues});
SELECT {AgentSelectColumns} FROM Agents WHERE Id = LAST_INSERT_ID();";

            var autoCommand = new CommandDefinition(autoSql, parameters, cancellationToken: cancellationToken);

            return await connection.QuerySingleAsync<Agent>(autoCommand);
        }

        var id = await IdentityHelper.GetNextIdAsync(connection, "Agents", cancellationToken);
        var manualSql = $@"INSERT INTO Agents (Id, {baseColumns})
VALUES (@Id, {baseValues});
SELECT {AgentSelectColumns} FROM Agents WHERE Id = @Id;";

        var manualParams = new
        {
            Id = Convert.ToInt32(id),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            CreatedAt = now,
            CreatedAtUtc = now
        };

        var manualCommand = new CommandDefinition(manualSql, manualParams, cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<Agent>(manualCommand);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Agents WHERE Id = @Id";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
