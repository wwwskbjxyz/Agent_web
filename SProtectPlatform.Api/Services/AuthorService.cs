using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Models;

namespace SProtectPlatform.Api.Services;

public interface IAuthorService
{
    Task<Author?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Author?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<AuthorAuthRecord?> GetAuthRecordByAccountAsync(string account, CancellationToken cancellationToken = default);
    Task<Author?> GetBySoftwareCodeAsync(string softwareCode, CancellationToken cancellationToken = default);
    Task<Author> CreateAsync(
        string username,
        string email,
        string passwordHash,
        string displayName,
        string apiAddress,
        int apiPort,
        string softwareType,
        CancellationToken cancellationToken = default);
    Task<Author?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthorSoftware>> GetSoftwaresAsync(int authorId, CancellationToken cancellationToken = default);
    Task<AuthorSoftware?> GetSoftwareByIdAsync(int authorId, int softwareId, CancellationToken cancellationToken = default);
    Task<AuthorSoftware> CreateSoftwareAsync(
        int authorId,
        string displayName,
        string apiAddress,
        int apiPort,
        string softwareType,
        CancellationToken cancellationToken = default);
    Task<AuthorSoftware?> UpdateSoftwareAsync(
        int authorId,
        int softwareId,
        string displayName,
        string apiAddress,
        int apiPort,
        string softwareType,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteSoftwareAsync(int authorId, int softwareId, CancellationToken cancellationToken = default);
    Task<string> RegenerateSoftwareCodeAsync(int authorId, int softwareId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class AuthorService : IAuthorService
{
    private readonly IMySqlConnectionFactory _connectionFactory;

    private const string StandaloneAuthorColumns = "CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode, COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) AS CreatedAt, 0 AS SoftwareId";
    private const string JoinedAuthorColumns = "CAST(a.Id AS SIGNED) AS Id, COALESCE(a.Username, a.Email) AS Username, a.Email, s.DisplayName, s.ApiAddress, s.ApiPort, s.SoftwareType, s.SoftwareCode, COALESCE(s.CreatedAtUtc, s.CreatedAt, a.CreatedAtUtc, a.CreatedAt, UTC_TIMESTAMP()) AS CreatedAt, CAST(s.Id AS SIGNED) AS SoftwareId";
    private const string SoftwareSelectColumns = "CAST(Id AS SIGNED) AS Id, CAST(AuthorId AS SIGNED) AS AuthorId, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode, COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) AS CreatedAt";

    public AuthorService(IMySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Author?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {StandaloneAuthorColumns} FROM Authors WHERE LOWER(Email) = @Email AND (IsDeleted = 0 OR IsDeleted IS NULL) LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Author>(sql, new { Email = email.ToLowerInvariant() });
    }

    public async Task<Author?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        if (!await IdentityHelper.ColumnExistsAsync(connection, "Authors", "Username", cancellationToken))
        {
            return null;
        }

        var sql = $"SELECT {StandaloneAuthorColumns} FROM Authors WHERE LOWER(Username) = @Username AND (IsDeleted = 0 OR IsDeleted IS NULL) LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<Author>(sql, new { Username = username.ToLowerInvariant() });
    }

    public async Task<AuthorAuthRecord?> GetAuthRecordByAccountAsync(string account, CancellationToken cancellationToken = default)
    {
        var normalized = account.ToLowerInvariant();
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        const string emailSql = "SELECT CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, PasswordHash FROM Authors WHERE LOWER(Email) = @Account AND (IsDeleted = 0 OR IsDeleted IS NULL) LIMIT 1";
        var record = await connection.QuerySingleOrDefaultAsync<AuthorAuthRecord>(emailSql, new { Account = normalized });

        if (record == null && await IdentityHelper.ColumnExistsAsync(connection, "Authors", "Username", cancellationToken))
        {
            const string usernameSql = "SELECT CAST(Id AS SIGNED) AS Id, COALESCE(Username, Email) AS Username, Email, PasswordHash FROM Authors WHERE LOWER(Username) = @Account AND (IsDeleted = 0 OR IsDeleted IS NULL) LIMIT 1";
            record = await connection.QuerySingleOrDefaultAsync<AuthorAuthRecord>(usernameSql, new { Account = normalized });
        }

        if (record == null)
        {
            return null;
        }

        var softwares = await connection.QueryAsync<AuthorSoftware>(
            new CommandDefinition($"SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE AuthorId = @AuthorId ORDER BY Id ASC", new
            {
                AuthorId = record.Id
            }, cancellationToken: cancellationToken));

        var softwareList = softwares.ToList();
        record.Softwares = softwareList;

        var primary = softwareList.FirstOrDefault();
        if (primary != null)
        {
            record.DisplayName = primary.DisplayName;
            record.ApiAddress = primary.ApiAddress;
            record.ApiPort = primary.ApiPort;
            record.SoftwareType = primary.SoftwareType;
            record.SoftwareCode = primary.SoftwareCode;
        }
        else
        {
            record.DisplayName = record.Email;
            record.ApiAddress = string.Empty;
            record.ApiPort = 0;
            record.SoftwareType = "SP";
            record.SoftwareCode = string.Empty;
        }

        return record;
    }

    public async Task<Author?> GetBySoftwareCodeAsync(string softwareCode, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {JoinedAuthorColumns} FROM Authors a INNER JOIN AuthorSoftwares s ON s.AuthorId = a.Id WHERE s.SoftwareCode = @SoftwareCode AND (a.IsDeleted = 0 OR a.IsDeleted IS NULL) LIMIT 1";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Author>(sql, new { SoftwareCode = softwareCode });
    }

    public async Task<Author> CreateAsync(
        string username,
        string email,
        string passwordHash,
        string displayName,
        string apiAddress,
        int apiPort,
        string softwareType,
        CancellationToken cancellationToken = default)
    {
        var softwareCode = GenerateSoftwareCode();
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var hasUsernameColumn = await IdentityHelper.ColumnExistsAsync(connection, "Authors", "Username", cancellationToken);
        var hasCreatedAtColumn = await IdentityHelper.ColumnExistsAsync(connection, "Authors", "CreatedAt", cancellationToken);
        var hasCreatedAtUtcColumn = await IdentityHelper.ColumnExistsAsync(connection, "Authors", "CreatedAtUtc", cancellationToken);
        var hasIsDeletedColumn = await IdentityHelper.ColumnExistsAsync(connection, "Authors", "IsDeleted", cancellationToken);

        var baseColumns = "Email, PasswordHash, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode";
        var baseValues = "@Email, @PasswordHash, @DisplayName, @ApiAddress, @ApiPort, @SoftwareType, @SoftwareCode";

        if (hasUsernameColumn)
        {
            baseColumns += ", Username";
            baseValues += ", @Username";
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

        if (hasIsDeletedColumn)
        {
            baseColumns += ", IsDeleted";
            baseValues += ", @IsDeleted";
        }

        var now = DateTime.UtcNow;
        Author author;

        if (await IdentityHelper.ColumnHasAutoIncrementAsync(connection, "Authors", cancellationToken))
        {
            var autoSql = $@"INSERT INTO Authors ({baseColumns})
VALUES ({baseValues});
SELECT {StandaloneAuthorColumns} FROM Authors WHERE Id = LAST_INSERT_ID();";

            author = await connection.QuerySingleAsync<Author>(
                new CommandDefinition(autoSql, new
                {
                    Username = username,
                    Email = email,
                    PasswordHash = passwordHash,
                    DisplayName = displayName,
                    ApiAddress = apiAddress,
                    ApiPort = apiPort,
                    SoftwareType = softwareType,
                    SoftwareCode = softwareCode,
                    CreatedAt = now,
                    CreatedAtUtc = now,
                    IsDeleted = 0
                }, cancellationToken: cancellationToken));
        }
        else
        {
            var id = await IdentityHelper.GetNextIdAsync(connection, "Authors", cancellationToken);
            var manualSql = $@"INSERT INTO Authors (Id, {baseColumns})
VALUES (@Id, {baseValues});
SELECT {StandaloneAuthorColumns} FROM Authors WHERE Id = @Id;";

            author = await connection.QuerySingleAsync<Author>(
                new CommandDefinition(manualSql, new
                {
                    Id = Convert.ToInt32(id),
                    Username = username,
                    Email = email,
                    PasswordHash = passwordHash,
                    DisplayName = displayName,
                    ApiAddress = apiAddress,
                    ApiPort = apiPort,
                    SoftwareType = softwareType,
                    SoftwareCode = softwareCode,
                    CreatedAt = now,
                    CreatedAtUtc = now,
                    IsDeleted = 0
                }, cancellationToken: cancellationToken));
        }

        await CreateSoftwareInternalAsync(connection, author.Id, displayName, apiAddress, apiPort, softwareType, softwareCode, cancellationToken);
        await SyncAuthorPrimarySoftwareAsync(connection, author.Id, cancellationToken);

        var synced = await GetByIdAsync(author.Id, cancellationToken);
        return synced ?? author;
    }

    public async Task<Author?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var author = await connection.QuerySingleOrDefaultAsync<Author>(
            new CommandDefinition($"SELECT {StandaloneAuthorColumns} FROM Authors WHERE Id = @Id AND (IsDeleted = 0 OR IsDeleted IS NULL) LIMIT 1", new { Id = id }, cancellationToken: cancellationToken));

        if (author == null)
        {
            return null;
        }

        var primary = await connection.QuerySingleOrDefaultAsync<AuthorSoftware>(
            new CommandDefinition($"SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE AuthorId = @AuthorId ORDER BY Id ASC LIMIT 1", new { AuthorId = author.Id }, cancellationToken: cancellationToken));

        if (primary != null)
        {
            author.DisplayName = primary.DisplayName;
            author.ApiAddress = primary.ApiAddress;
            author.ApiPort = primary.ApiPort;
            author.SoftwareType = primary.SoftwareType;
            author.SoftwareCode = primary.SoftwareCode;
            author.SoftwareId = primary.Id;
        }

        return author;
    }

    public async Task<IReadOnlyList<AuthorSoftware>> GetSoftwaresAsync(int authorId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<AuthorSoftware>(
            new CommandDefinition($"SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE AuthorId = @AuthorId ORDER BY Id ASC", new { AuthorId = authorId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<AuthorSoftware?> GetSoftwareByIdAsync(int authorId, int softwareId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthorSoftware>(
            new CommandDefinition($"SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE Id = @SoftwareId AND AuthorId = @AuthorId LIMIT 1", new
            {
                SoftwareId = softwareId,
                AuthorId = authorId
            }, cancellationToken: cancellationToken));
    }

    public async Task<AuthorSoftware> CreateSoftwareAsync(int authorId, string displayName, string apiAddress, int apiPort, string softwareType, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var software = await CreateSoftwareInternalAsync(connection, authorId, displayName, apiAddress, apiPort, softwareType, null, cancellationToken);
        await SyncAuthorPrimarySoftwareAsync(connection, authorId, cancellationToken);
        return software;
    }

    public async Task<AuthorSoftware?> UpdateSoftwareAsync(int authorId, int softwareId, string displayName, string apiAddress, int apiPort, string softwareType, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE AuthorSoftwares
SET DisplayName = @DisplayName,
    ApiAddress = @ApiAddress,
    ApiPort = @ApiPort,
    SoftwareType = @SoftwareType
WHERE Id = @Id AND AuthorId = @AuthorId";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = softwareId,
            AuthorId = authorId,
            DisplayName = displayName,
            ApiAddress = apiAddress,
            ApiPort = apiPort,
            SoftwareType = softwareType
        }, cancellationToken: cancellationToken));

        if (affected <= 0)
        {
            return null;
        }

        await SyncAuthorPrimarySoftwareAsync(connection, authorId, cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthorSoftware>(
            new CommandDefinition($"SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE Id = @SoftwareId", new { SoftwareId = softwareId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteSoftwareAsync(int authorId, int softwareId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string countSql = "SELECT COUNT(*) FROM AuthorSoftwares WHERE AuthorId = @AuthorId";
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, new { AuthorId = authorId }, cancellationToken: cancellationToken));
        if (count <= 1)
        {
            return false;
        }

        const string deleteSql = "DELETE FROM AuthorSoftwares WHERE Id = @Id AND AuthorId = @AuthorId";
        var rows = await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { Id = softwareId, AuthorId = authorId }, cancellationToken: cancellationToken));
        if (rows <= 0)
        {
            return false;
        }

        await SyncAuthorPrimarySoftwareAsync(connection, authorId, cancellationToken);
        return true;
    }

    public async Task<string> RegenerateSoftwareCodeAsync(int authorId, int softwareId, CancellationToken cancellationToken = default)
    {
        var newCode = GenerateSoftwareCode();
        const string sql = "UPDATE AuthorSoftwares SET SoftwareCode = @SoftwareCode WHERE Id = @Id AND AuthorId = @AuthorId";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = softwareId,
            AuthorId = authorId,
            SoftwareCode = newCode
        }, cancellationToken: cancellationToken));

        if (affected <= 0)
        {
            throw new InvalidOperationException("软件不存在或已删除");
        }

        await SyncAuthorPrimarySoftwareAsync(connection, authorId, cancellationToken);
        return newCode;
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Authors SET IsDeleted = 1 WHERE Id = @Id AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    private async Task<AuthorSoftware> CreateSoftwareInternalAsync(
        IDbConnection connection,
        int authorId,
        string displayName,
        string apiAddress,
        int apiPort,
        string softwareType,
        string? existingSoftwareCode,
        CancellationToken cancellationToken)
    {
        var hasCreatedAt = await IdentityHelper.ColumnExistsAsync(connection, "AuthorSoftwares", "CreatedAt", cancellationToken);
        var hasCreatedAtUtc = await IdentityHelper.ColumnExistsAsync(connection, "AuthorSoftwares", "CreatedAtUtc", cancellationToken);
        var columns = "AuthorId, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode";
        var values = "@AuthorId, @DisplayName, @ApiAddress, @ApiPort, @SoftwareType, @SoftwareCode";

        if (hasCreatedAt)
        {
            columns += ", CreatedAt";
            values += ", @CreatedAt";
        }

        if (hasCreatedAtUtc)
        {
            columns += ", CreatedAtUtc";
            values += ", @CreatedAtUtc";
        }

        var now = DateTime.UtcNow;
        var attempt = 0;

        while (true)
        {
            attempt++;
            var code = existingSoftwareCode ?? GenerateSoftwareCode();
            var parameters = new
            {
                AuthorId = authorId,
                DisplayName = displayName,
                ApiAddress = apiAddress,
                ApiPort = apiPort,
                SoftwareType = softwareType,
                SoftwareCode = code,
                CreatedAt = now,
                CreatedAtUtc = now
            };

            try
            {
                if (await IdentityHelper.ColumnHasAutoIncrementAsync(connection, "AuthorSoftwares", cancellationToken))
                {
                    var autoSql = $@"INSERT INTO AuthorSoftwares ({columns})
VALUES ({values});
SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE Id = LAST_INSERT_ID();";

                    return await connection.QuerySingleAsync<AuthorSoftware>(new CommandDefinition(autoSql, parameters, cancellationToken: cancellationToken));
                }

                var id = await IdentityHelper.GetNextIdAsync(connection, "AuthorSoftwares", cancellationToken);
                var manualSql = $@"INSERT INTO AuthorSoftwares (Id, {columns})
VALUES (@Id, {values});
SELECT {SoftwareSelectColumns} FROM AuthorSoftwares WHERE Id = @Id;";

                var manualParams = new
                {
                    Id = Convert.ToInt32(id),
                    AuthorId = authorId,
                    DisplayName = displayName,
                    ApiAddress = apiAddress,
                    ApiPort = apiPort,
                    SoftwareType = softwareType,
                    SoftwareCode = code,
                    CreatedAt = now,
                    CreatedAtUtc = now
                };

                return await connection.QuerySingleAsync<AuthorSoftware>(new CommandDefinition(manualSql, manualParams, cancellationToken: cancellationToken));
            }
            catch (MySqlException ex) when (ex.Number == 1062 && existingSoftwareCode == null && attempt < 5)
            {
                continue;
            }
        }
    }

    private static async Task SyncAuthorPrimarySoftwareAsync(IDbConnection connection, int authorId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode FROM AuthorSoftwares WHERE AuthorId = @AuthorId ORDER BY Id ASC LIMIT 1";
        var primary = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(sql, new { AuthorId = authorId }, cancellationToken: cancellationToken));
        if (primary == null)
        {
            return;
        }

        const string updateSql = @"UPDATE Authors
SET DisplayName = @DisplayName,
    ApiAddress = @ApiAddress,
    ApiPort = @ApiPort,
    SoftwareType = @SoftwareType,
    SoftwareCode = @SoftwareCode
WHERE Id = @AuthorId";

        await connection.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            AuthorId = authorId,
            DisplayName = (string)primary.DisplayName,
            ApiAddress = (string)primary.ApiAddress,
            ApiPort = (int)primary.ApiPort,
            SoftwareType = (string)primary.SoftwareType,
            SoftwareCode = (string)primary.SoftwareCode
        }, cancellationToken: cancellationToken));
    }

    private static string GenerateSoftwareCode() => $"SP-{Guid.NewGuid():N}".ToUpperInvariant();
}
