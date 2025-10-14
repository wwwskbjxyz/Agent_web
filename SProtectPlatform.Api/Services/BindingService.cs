using System;
using System.Linq;
using Dapper;
using SProtectPlatform.Api.Data;

namespace SProtectPlatform.Api.Services;

public interface IBindingService
{
    Task<IReadOnlyCollection<BindingRecord>> GetBindingsForAgentAsync(int agentId, CancellationToken cancellationToken = default);
    Task<BindingRecord?> GetBindingAsync(int agentId, string softwareCode, CancellationToken cancellationToken = default);
    Task<BindingRecord?> GetBindingBySoftwareCodeAsync(string softwareCode, CancellationToken cancellationToken = default);
    Task<BindingRecord> CreateAsync(
        int agentId,
        int authorId,
        int authorSoftwareId,
        string softwareCode,
        string authorAccount,
        string encryptedAuthorAccount,
        string encryptedPassword,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int agentId, int bindingId, CancellationToken cancellationToken = default);
    Task<BindingRecord?> UpdateCredentialsAsync(
        int agentId,
        int bindingId,
        string authorAccount,
        string encryptedAuthorAccount,
        string encryptedPassword,
        CancellationToken cancellationToken = default);
}

public sealed class BindingRecord
{
    public int BindingId { get; set; }
    public int AgentId { get; set; }
    public int AuthorId { get; set; }
    public int AuthorSoftwareId { get; set; }
    public string AuthorAccount { get; set; } = string.Empty;
    public string EncryptedAuthorAccount { get; set; } = string.Empty;
    public string EncryptedAuthorPassword { get; set; } = string.Empty;
    public string SoftwareCode { get; set; } = string.Empty;
    public string SoftwareType { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string ApiAddress { get; set; } = string.Empty;
    public int ApiPort { get; set; }
}

public sealed class BindingService : IBindingService
{
    private readonly IMySqlConnectionFactory _connectionFactory;

    public BindingService(IMySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyCollection<BindingRecord>> GetBindingsForAgentAsync(int agentId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT CAST(b.Id AS SIGNED) AS BindingId,
       CAST(b.AgentId AS SIGNED) AS AgentId,
       CAST(b.AuthorId AS SIGNED) AS AuthorId,
       CAST(COALESCE(b.AuthorSoftwareId, s.Id) AS SIGNED) AS AuthorSoftwareId,
       b.AuthorAccount,
       b.EncryptedAuthorAccount,
       b.EncryptedAuthorPassword,
       COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) AS SoftwareCode,
       COALESCE(s.SoftwareType, a.SoftwareType) AS SoftwareType,
       COALESCE(s.DisplayName, a.DisplayName) AS AuthorDisplayName,
       a.Email AS AuthorEmail,
       COALESCE(s.ApiAddress, a.ApiAddress) AS ApiAddress,
        COALESCE(s.ApiPort, a.ApiPort) AS ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.Id = b.AuthorSoftwareId
WHERE b.AgentId = @AgentId AND (a.IsDeleted = 0 OR a.IsDeleted IS NULL)
ORDER BY b.Id DESC";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<BindingRecord>(sql, new { AgentId = agentId });
        return items.ToList();
    }

    public async Task<BindingRecord?> GetBindingAsync(int agentId, string softwareCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT CAST(b.Id AS SIGNED) AS BindingId,
       CAST(b.AgentId AS SIGNED) AS AgentId,
       CAST(b.AuthorId AS SIGNED) AS AuthorId,
       CAST(COALESCE(b.AuthorSoftwareId, s.Id) AS SIGNED) AS AuthorSoftwareId,
       b.AuthorAccount,
       b.EncryptedAuthorAccount,
       b.EncryptedAuthorPassword,
       COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) AS SoftwareCode,
       COALESCE(s.SoftwareType, a.SoftwareType) AS SoftwareType,
       COALESCE(s.DisplayName, a.DisplayName) AS AuthorDisplayName,
       a.Email AS AuthorEmail,
       COALESCE(s.ApiAddress, a.ApiAddress) AS ApiAddress,
       COALESCE(s.ApiPort, a.ApiPort) AS ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.Id = b.AuthorSoftwareId OR s.SoftwareCode = @SoftwareCode
WHERE b.AgentId = @AgentId AND (COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) = @SoftwareCode) AND (a.IsDeleted = 0 OR a.IsDeleted IS NULL)
LIMIT 1";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BindingRecord>(sql, new { AgentId = agentId, SoftwareCode = softwareCode });
    }

    public async Task<BindingRecord?> GetBindingBySoftwareCodeAsync(string softwareCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT CAST(b.Id AS SIGNED) AS BindingId,
       CAST(b.AgentId AS SIGNED) AS AgentId,
       CAST(b.AuthorId AS SIGNED) AS AuthorId,
       CAST(COALESCE(b.AuthorSoftwareId, s.Id) AS SIGNED) AS AuthorSoftwareId,
       b.AuthorAccount,
       b.EncryptedAuthorAccount,
       b.EncryptedAuthorPassword,
       COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) AS SoftwareCode,
       COALESCE(s.SoftwareType, a.SoftwareType) AS SoftwareType,
       COALESCE(s.DisplayName, a.DisplayName) AS AuthorDisplayName,
       a.Email AS AuthorEmail,
       COALESCE(s.ApiAddress, a.ApiAddress) AS ApiAddress,
       COALESCE(s.ApiPort, a.ApiPort) AS ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.Id = b.AuthorSoftwareId OR s.SoftwareCode = @SoftwareCode
WHERE (COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) = @SoftwareCode) AND (a.IsDeleted = 0 OR a.IsDeleted IS NULL)
ORDER BY b.Id DESC
LIMIT 1";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BindingRecord>(sql, new { SoftwareCode = softwareCode });
    }

    public async Task<BindingRecord> CreateAsync(
        int agentId,
        int authorId,
        int authorSoftwareId,
        string softwareCode,
        string authorAccount,
        string encryptedAuthorAccount,
        string encryptedPassword,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var hasCreatedAtColumn = await IdentityHelper.ColumnExistsAsync(connection, "Bindings", "CreatedAt", cancellationToken);
        var hasCreatedAtUtcColumn = await IdentityHelper.ColumnExistsAsync(connection, "Bindings", "CreatedAtUtc", cancellationToken);
        var hasEncryptedAccountColumn = await IdentityHelper.ColumnExistsAsync(connection, "Bindings", "EncryptedAuthorAccount", cancellationToken);
        var baseColumns = "AgentId, AuthorId, AuthorSoftwareId, SoftwareCode, AuthorAccount, EncryptedAuthorAccount, EncryptedAuthorPassword";
        var baseValues = "@AgentId, @AuthorId, @AuthorSoftwareId, @SoftwareCode, @AuthorAccount, @EncryptedAuthorAccount, @EncryptedAuthorPassword";

        if (!hasEncryptedAccountColumn)
        {
            baseColumns = "AgentId, AuthorId, AuthorSoftwareId, SoftwareCode, AuthorAccount, EncryptedAuthorPassword";
            baseValues = "@AgentId, @AuthorId, @AuthorSoftwareId, @SoftwareCode, @AuthorAccount, @EncryptedAuthorPassword";
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

        var parameters = new
        {
            AgentId = agentId,
            AuthorId = authorId,
            AuthorSoftwareId = authorSoftwareId,
            SoftwareCode = softwareCode,
            AuthorAccount = authorAccount,
            EncryptedAuthorAccount = encryptedAuthorAccount,
            EncryptedAuthorPassword = encryptedPassword,
            CreatedAt = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (await IdentityHelper.ColumnHasAutoIncrementAsync(connection, "Bindings", cancellationToken))
        {
            var autoSql = $@"INSERT INTO Bindings ({baseColumns})
VALUES ({baseValues});
SELECT CAST(b.Id AS SIGNED) AS BindingId,
       CAST(b.AgentId AS SIGNED) AS AgentId,
       CAST(b.AuthorId AS SIGNED) AS AuthorId,
       CAST(COALESCE(b.AuthorSoftwareId, s.Id) AS SIGNED) AS AuthorSoftwareId,
       b.AuthorAccount,
       b.EncryptedAuthorAccount,
       b.EncryptedAuthorPassword,
       COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) AS SoftwareCode,
       COALESCE(s.SoftwareType, a.SoftwareType) AS SoftwareType,
       COALESCE(s.DisplayName, a.DisplayName) AS AuthorDisplayName,
       a.Email AS AuthorEmail,
       COALESCE(s.ApiAddress, a.ApiAddress) AS ApiAddress,
       COALESCE(s.ApiPort, a.ApiPort) AS ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.Id = b.AuthorSoftwareId
WHERE b.Id = LAST_INSERT_ID();";

            var autoCommand = new CommandDefinition(autoSql, parameters, cancellationToken: cancellationToken);

            return await connection.QuerySingleAsync<BindingRecord>(autoCommand);
        }

        var id = await IdentityHelper.GetNextIdAsync(connection, "Bindings", cancellationToken);
        var manualSql = $@"INSERT INTO Bindings (Id, {baseColumns})
VALUES (@Id, {baseValues});
SELECT CAST(b.Id AS SIGNED) AS BindingId,
       CAST(b.AgentId AS SIGNED) AS AgentId,
       CAST(b.AuthorId AS SIGNED) AS AuthorId,
       CAST(COALESCE(b.AuthorSoftwareId, s.Id) AS SIGNED) AS AuthorSoftwareId,
       b.AuthorAccount,
       b.EncryptedAuthorAccount,
       b.EncryptedAuthorPassword,
       COALESCE(b.SoftwareCode, s.SoftwareCode, a.SoftwareCode) AS SoftwareCode,
       COALESCE(s.SoftwareType, a.SoftwareType) AS SoftwareType,
       COALESCE(s.DisplayName, a.DisplayName) AS AuthorDisplayName,
       a.Email AS AuthorEmail,
       COALESCE(s.ApiAddress, a.ApiAddress) AS ApiAddress,
       COALESCE(s.ApiPort, a.ApiPort) AS ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.Id = b.AuthorSoftwareId
WHERE b.Id = @Id;";

        var manualParams = new
        {
            Id = Convert.ToInt32(id),
            AgentId = agentId,
            AuthorId = authorId,
            AuthorSoftwareId = authorSoftwareId,
            SoftwareCode = softwareCode,
            AuthorAccount = authorAccount,
            EncryptedAuthorAccount = encryptedAuthorAccount,
            EncryptedAuthorPassword = encryptedPassword,
            CreatedAt = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        var manualCommand = new CommandDefinition(manualSql, manualParams, cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<BindingRecord>(manualCommand);
    }

    public async Task<bool> DeleteAsync(int agentId, int bindingId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Bindings WHERE Id = @Id AND AgentId = @AgentId";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(sql, new { Id = bindingId, AgentId = agentId });
        return rows > 0;
    }

    public async Task<BindingRecord?> UpdateCredentialsAsync(
        int agentId,
        int bindingId,
        string authorAccount,
        string encryptedAuthorAccount,
        string encryptedPassword,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Bindings
SET AuthorAccount = @AuthorAccount,
    EncryptedAuthorAccount = @EncryptedAuthorAccount,
    EncryptedAuthorPassword = @EncryptedAuthorPassword
WHERE Id = @Id AND AgentId = @AgentId";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = bindingId,
            AgentId = agentId,
            AuthorAccount = authorAccount,
            EncryptedAuthorAccount = encryptedAuthorAccount,
            EncryptedAuthorPassword = encryptedPassword
        }, cancellationToken: cancellationToken));

        if (affected <= 0)
        {
            return null;
        }

        const string fetchSql = @"SELECT CAST(b.Id AS SIGNED) AS BindingId, CAST(b.AgentId AS SIGNED) AS AgentId, CAST(b.AuthorId AS SIGNED) AS AuthorId, b.AuthorAccount, b.EncryptedAuthorAccount, b.EncryptedAuthorPassword,
       a.SoftwareCode, a.SoftwareType, a.DisplayName AS AuthorDisplayName, a.Email AS AuthorEmail, a.ApiAddress, a.ApiPort
FROM Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
WHERE b.Id = @Id AND b.AgentId = @AgentId AND (a.IsDeleted = 0 OR a.IsDeleted IS NULL)
LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<BindingRecord>(
            new CommandDefinition(fetchSql, new { Id = bindingId, AgentId = agentId }, cancellationToken: cancellationToken));
    }
}
