using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using MySqlConnector;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SProtectPlatform.Api.Data;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IMySqlConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IMySqlConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        const string authorsSql = @"CREATE TABLE IF NOT EXISTS Authors (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            Username VARCHAR(191) NOT NULL UNIQUE,
            Email VARCHAR(191) NOT NULL UNIQUE,
            PasswordHash VARCHAR(255) NOT NULL,
            DisplayName VARCHAR(255) NOT NULL,
            ApiAddress VARCHAR(255) NOT NULL,
            ApiPort INT NOT NULL,
            SoftwareType VARCHAR(50) NOT NULL,
            SoftwareCode VARCHAR(100) NOT NULL UNIQUE,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        const string agentsSql = @"CREATE TABLE IF NOT EXISTS Agents (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            Username VARCHAR(191) NOT NULL UNIQUE,
            Email VARCHAR(191) NOT NULL UNIQUE,
            PasswordHash VARCHAR(255) NOT NULL,
            DisplayName VARCHAR(255) NOT NULL,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        const string bindingsSql = @"CREATE TABLE IF NOT EXISTS Bindings (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            AgentId INT NOT NULL,
            AuthorId INT NOT NULL,
            AuthorAccount VARCHAR(255) NOT NULL,
            EncryptedAuthorAccount TEXT NOT NULL,
            EncryptedAuthorPassword TEXT NOT NULL,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (AgentId) REFERENCES Agents(Id) ON DELETE CASCADE,
            FOREIGN KEY (AuthorId) REFERENCES Authors(Id) ON DELETE CASCADE
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        const string originsSql = @"CREATE TABLE IF NOT EXISTS AllowedOrigins (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            Origin VARCHAR(191) NOT NULL UNIQUE,
            Description VARCHAR(255),
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "Authors", authorsSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "Authors", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "Username", "Username VARCHAR(191) NULL AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "Email", "Email VARCHAR(191) NOT NULL UNIQUE AFTER Username", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "PasswordHash", "PasswordHash VARCHAR(255) NOT NULL AFTER Email", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "DisplayName", "DisplayName VARCHAR(255) NOT NULL DEFAULT '' AFTER PasswordHash", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "ApiAddress", "ApiAddress VARCHAR(255) NOT NULL AFTER DisplayName", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "ApiPort", "ApiPort INT NOT NULL AFTER ApiAddress", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "SoftwareType", "SoftwareType VARCHAR(50) NOT NULL AFTER ApiPort", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "SoftwareCode", "SoftwareCode VARCHAR(100) NOT NULL UNIQUE AFTER SoftwareType", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "CreatedAt", "CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER SoftwareCode", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER CreatedAt", cancellationToken);
        await EnsureColumnAsync(connection, "Authors", "IsDeleted", "IsDeleted TINYINT(1) NOT NULL DEFAULT 0 AFTER CreatedAtUtc", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Authors", "CreatedAt", "CURRENT_TIMESTAMP", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Authors", "CreatedAtUtc", "UTC_TIMESTAMP()", cancellationToken);
        await NormalizeAuthorDataAsync(connection, cancellationToken);
        await EnsureIndexAsync(connection, "Authors", "UX_Authors_Username", new[] { "Username" }, unique: true, cancellationToken);

        const string authorSoftwaresSql = @"CREATE TABLE IF NOT EXISTS AuthorSoftwares (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            AuthorId INT NOT NULL,
            DisplayName VARCHAR(255) NOT NULL,
            ApiAddress VARCHAR(255) NOT NULL,
            ApiPort INT NOT NULL,
            SoftwareType VARCHAR(50) NOT NULL,
            SoftwareCode VARCHAR(100) NOT NULL UNIQUE,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (AuthorId) REFERENCES Authors(Id) ON DELETE CASCADE
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        const string authorSoftwaresFallbackSql = @"CREATE TABLE IF NOT EXISTS AuthorSoftwares (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            AuthorId INT NOT NULL,
            DisplayName VARCHAR(255) NOT NULL,
            ApiAddress VARCHAR(255) NOT NULL,
            ApiPort INT NOT NULL,
            SoftwareType VARCHAR(50) NOT NULL,
            SoftwareCode VARCHAR(100) NOT NULL UNIQUE,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "AuthorSoftwares", authorSoftwaresSql, authorSoftwaresFallbackSql, cancellationToken);
        await EnsureIdColumnAsync(connection, "AuthorSoftwares", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "AuthorId", "AuthorId INT NOT NULL AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "DisplayName", "DisplayName VARCHAR(255) NOT NULL AFTER AuthorId", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "ApiAddress", "ApiAddress VARCHAR(255) NOT NULL AFTER DisplayName", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "ApiPort", "ApiPort INT NOT NULL AFTER ApiAddress", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "SoftwareType", "SoftwareType VARCHAR(50) NOT NULL AFTER ApiPort", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "SoftwareCode", "SoftwareCode VARCHAR(100) NOT NULL UNIQUE AFTER SoftwareType", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "CreatedAt", "CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER SoftwareCode", cancellationToken);
        await EnsureColumnAsync(connection, "AuthorSoftwares", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER CreatedAt", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "AuthorSoftwares", "CreatedAt", "CURRENT_TIMESTAMP", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "AuthorSoftwares", "CreatedAtUtc", "UTC_TIMESTAMP()", cancellationToken);
        await EnsureIndexAsync(connection, "AuthorSoftwares", "IDX_AuthorSoftwares_AuthorId", new[] { "AuthorId" }, unique: false, cancellationToken);
        await EnsureIndexAsync(connection, "AuthorSoftwares", "UX_AuthorSoftwares_Code", new[] { "SoftwareCode" }, unique: true, cancellationToken);
        await BackfillAuthorSoftwaresAsync(connection, cancellationToken);

        await ExecuteCreateTableAsync(connection, "Agents", agentsSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "Agents", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "Username", "Username VARCHAR(191) NULL AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "Email", "Email VARCHAR(191) NOT NULL UNIQUE AFTER Username", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "PasswordHash", "PasswordHash VARCHAR(255) NOT NULL AFTER Email", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "DisplayName", "DisplayName VARCHAR(255) NOT NULL DEFAULT '' AFTER PasswordHash", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "CreatedAt", "CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER DisplayName", cancellationToken);
        await EnsureColumnAsync(connection, "Agents", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER CreatedAt", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Agents", "CreatedAt", "CURRENT_TIMESTAMP", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Agents", "CreatedAtUtc", "UTC_TIMESTAMP()", cancellationToken);
        await NormalizeAgentDataAsync(connection, cancellationToken);
        await EnsureIndexAsync(connection, "Agents", "UX_Agents_Username", new[] { "Username" }, unique: true, cancellationToken);
        const string bindingsFallbackSql = @"CREATE TABLE IF NOT EXISTS Bindings (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            AgentId INT NOT NULL,
            AuthorId INT NOT NULL,
            AuthorAccount VARCHAR(255) NOT NULL,
            EncryptedAuthorAccount TEXT NOT NULL,
            EncryptedAuthorPassword TEXT NOT NULL,
            CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "Bindings", bindingsSql, bindingsFallbackSql, cancellationToken);
        await EnsureIdColumnAsync(connection, "Bindings", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "AuthorAccount", "AuthorAccount VARCHAR(255) NOT NULL AFTER AuthorId", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "AuthorSoftwareId", "AuthorSoftwareId INT NULL AFTER AuthorId", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "SoftwareCode", "SoftwareCode VARCHAR(100) NULL AFTER AuthorSoftwareId", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "EncryptedAuthorAccount", "EncryptedAuthorAccount TEXT NOT NULL AFTER AuthorAccount", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "EncryptedAuthorPassword", "EncryptedAuthorPassword TEXT NOT NULL AFTER EncryptedAuthorAccount", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "CreatedAt", "CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER EncryptedAuthorPassword", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Bindings", "CreatedAt", "CURRENT_TIMESTAMP", cancellationToken);
        await EnsureColumnAsync(connection, "Bindings", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER CreatedAt", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "Bindings", "CreatedAtUtc", "UTC_TIMESTAMP()", cancellationToken);
        await NormalizeBindingDataAsync(connection, cancellationToken);
        await BackfillBindingSoftwareDataAsync(connection, cancellationToken);
        await EnsureIndexAsync(connection, "Bindings", "UX_Bindings", new[] { "AgentId", "AuthorSoftwareId", "SoftwareCode" }, unique: true, cancellationToken);

        const string wechatBindingsSql = @"CREATE TABLE IF NOT EXISTS WeChatBindings (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            UserType VARCHAR(32) NOT NULL,
            UserId INT NOT NULL,
            OpenId VARCHAR(128) NOT NULL,
            UnionId VARCHAR(128) NULL,
            Nickname VARCHAR(191) NULL,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UpdatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            UNIQUE KEY UX_WeChatBindings_User (UserType, UserId),
            UNIQUE KEY UX_WeChatBindings_OpenId (OpenId)
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "WeChatBindings", wechatBindingsSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "WeChatBindings", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "UserType", "UserType VARCHAR(32) NOT NULL AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "UserId", "UserId INT NOT NULL AFTER UserType", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "OpenId", "OpenId VARCHAR(128) NOT NULL AFTER UserId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "UnionId", "UnionId VARCHAR(128) NULL AFTER OpenId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "Nickname", "Nickname VARCHAR(191) NULL AFTER UnionId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER Nickname", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatBindings", "UpdatedAtUtc", "UpdatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER CreatedAtUtc", cancellationToken);
        await EnsureIndexAsync(connection, "WeChatBindings", "UX_WeChatBindings_User", new[] { "UserType", "UserId" }, unique: true, cancellationToken);
        await EnsureIndexAsync(connection, "WeChatBindings", "UX_WeChatBindings_OpenId", new[] { "OpenId" }, unique: true, cancellationToken);

        const string wechatTokensSql = @"CREATE TABLE IF NOT EXISTS WeChatAccessTokens (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            AppId VARCHAR(64) NOT NULL UNIQUE,
            AccessToken VARCHAR(512) NOT NULL,
            ExpiresAtUtc DATETIME NOT NULL,
            UpdatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "WeChatAccessTokens", wechatTokensSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "WeChatAccessTokens", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatAccessTokens", "AppId", "AppId VARCHAR(64) NOT NULL UNIQUE AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatAccessTokens", "AccessToken", "AccessToken VARCHAR(512) NOT NULL AFTER AppId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatAccessTokens", "ExpiresAtUtc", "ExpiresAtUtc DATETIME NOT NULL AFTER AccessToken", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatAccessTokens", "UpdatedAtUtc", "UpdatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER ExpiresAtUtc", cancellationToken);
        await EnsureIndexAsync(connection, "WeChatAccessTokens", "UX_WeChatAccessTokens_AppId", new[] { "AppId" }, unique: true, cancellationToken);

        const string wechatLogsSql = @"CREATE TABLE IF NOT EXISTS WeChatMessageLogs (
            Id INT AUTO_INCREMENT PRIMARY KEY,
            TemplateKey VARCHAR(64) NOT NULL,
            TemplateId VARCHAR(128) NOT NULL,
            UserType VARCHAR(32) NOT NULL,
            UserId INT NOT NULL,
            OpenId VARCHAR(128) NOT NULL,
            PayloadJson TEXT NOT NULL,
            Success TINYINT(1) NOT NULL DEFAULT 0,
            ErrorCode INT NOT NULL DEFAULT 0,
            ErrorMessage TEXT NULL,
            CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await ExecuteCreateTableAsync(connection, "WeChatMessageLogs", wechatLogsSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "WeChatMessageLogs", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "TemplateKey", "TemplateKey VARCHAR(64) NOT NULL AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "TemplateId", "TemplateId VARCHAR(128) NOT NULL AFTER TemplateKey", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "UserType", "UserType VARCHAR(32) NOT NULL AFTER TemplateId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "UserId", "UserId INT NOT NULL AFTER UserType", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "OpenId", "OpenId VARCHAR(128) NOT NULL AFTER UserId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "PayloadJson", "PayloadJson TEXT NOT NULL AFTER OpenId", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "Success", "Success TINYINT(1) NOT NULL DEFAULT 0 AFTER PayloadJson", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "ErrorCode", "ErrorCode INT NOT NULL DEFAULT 0 AFTER Success", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "ErrorMessage", "ErrorMessage TEXT NULL AFTER ErrorCode", cancellationToken);
        await EnsureColumnAsync(connection, "WeChatMessageLogs", "CreatedAtUtc", "CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER ErrorMessage", cancellationToken);
        await EnsureIndexAsync(connection, "WeChatMessageLogs", "IDX_WeChatMessageLogs_User", new[] { "UserType", "UserId" }, unique: false, cancellationToken);

        await ExecuteCreateTableAsync(connection, "AllowedOrigins", originsSql, null, cancellationToken);
        await EnsureIdColumnAsync(connection, "AllowedOrigins", cancellationToken);
        await EnsureColumnAsync(connection, "AllowedOrigins", "Origin", "Origin VARCHAR(191) NOT NULL UNIQUE AFTER Id", cancellationToken);
        await EnsureColumnAsync(connection, "AllowedOrigins", "Description", "Description VARCHAR(255) NULL AFTER Origin", cancellationToken);
        await EnsureColumnAsync(connection, "AllowedOrigins", "CreatedAt", "CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER Description", cancellationToken);
        await EnsureColumnDefaultAsync(connection, "AllowedOrigins", "CreatedAt", "CURRENT_TIMESTAMP", cancellationToken);

        _logger.LogInformation("Database schema ensured.");
    }

    private static async Task EnsureColumnAsync(IDbConnection connection, string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        const string existsSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND LOWER(COLUMN_NAME) = LOWER(@Column)";

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(existsSql, new
        {
            Table = tableName,
            Column = columnName
        }, cancellationToken: cancellationToken));

        if (exists > 0)
        {
            return;
        }

        var alterSql = $"ALTER TABLE `{tableName}` ADD COLUMN {columnDefinition}";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(alterSql, cancellationToken: cancellationToken));
        }
        catch (MySqlException ex) when (ex.Number == 1060)
        {
            // Column already exists but INFORMATION_SCHEMA comparisons may have failed due to
            // legacy collation differences. We can safely ignore the duplicate-column error.
        }
    }

    private static async Task EnsureColumnDefaultAsync(IDbConnection connection, string tableName, string columnName, string defaultExpression, CancellationToken cancellationToken)
    {
        var column = await GetColumnMetadataAsync(connection, tableName, columnName, cancellationToken);

        if (column is null)
        {
            return;
        }

        if (DefaultMatches(column.ColumnDefault, defaultExpression))
        {
            return;
        }

        var nullClause = string.Equals(column.IsNullable, "YES", StringComparison.OrdinalIgnoreCase) ? "NULL" : "NOT NULL";
        var defaultClause = string.IsNullOrWhiteSpace(defaultExpression) ? string.Empty : $" DEFAULT {defaultExpression}";
        var extraClause = string.IsNullOrWhiteSpace(column.Extra) ? string.Empty : $" {column.Extra}";

        var sql = $"ALTER TABLE `{tableName}` MODIFY COLUMN `{columnName}` {column.ColumnType} {nullClause}{defaultClause}{extraClause}";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
        }
        catch (Exception)
        {
            // Some managed database instances may not permit ALTER COLUMN operations. In that case we fall back
            // to the existing definition and rely on explicit values supplied during inserts.
        }
    }

    private static bool DefaultMatches(string? currentDefault, string desiredDefault)
    {
        var normalizedCurrent = NormalizeDefault(currentDefault);
        var normalizedDesired = NormalizeDefault(desiredDefault);
        return !string.IsNullOrEmpty(normalizedCurrent) && normalizedCurrent == normalizedDesired;
    }

    private static string NormalizeDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Trim('(', ')').Trim();
        return trimmed.Trim('"', '\'', '`').ToLowerInvariant();
    }

    private static async Task<ColumnMetadata?> GetColumnMetadataAsync(IDbConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT COLUMN_TYPE AS ColumnType, COLUMN_DEFAULT AS ColumnDefault, IS_NULLABLE AS IsNullable, EXTRA AS Extra
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND LOWER(COLUMN_NAME) = LOWER(@Column);";

        return await connection.QuerySingleOrDefaultAsync<ColumnMetadata>(new CommandDefinition(sql, new
        {
            Table = tableName,
            Column = columnName
        }, cancellationToken: cancellationToken));
    }

    private static async Task EnsureIdColumnAsync(IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        if (await IdentityHelper.ColumnExistsAsync(connection, tableName, "Id", cancellationToken))
        {
            const string hasPrimarySql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND CONSTRAINT_TYPE = 'PRIMARY KEY';";

            var hasPrimary = await connection.ExecuteScalarAsync<int>(new CommandDefinition(hasPrimarySql, new
            {
                Table = tableName
            }, cancellationToken: cancellationToken));

            if (hasPrimary == 0)
            {
                var addPrimarySql = $"ALTER TABLE `{tableName}` ADD PRIMARY KEY (`Id`)";
                await connection.ExecuteAsync(new CommandDefinition(addPrimarySql, cancellationToken: cancellationToken));
            }

            return;
        }

        var addSql = $"ALTER TABLE `{tableName}` ADD COLUMN Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY FIRST";
        await connection.ExecuteAsync(new CommandDefinition(addSql, cancellationToken: cancellationToken));
    }

    private static async Task NormalizeAuthorDataAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (await IdentityHelper.ColumnExistsAsync(connection, "Authors", "Username", cancellationToken))
        {
            var sync = new CommandDefinition("UPDATE Authors SET Username = Email WHERE Username IS NULL OR Username = '';", cancellationToken: cancellationToken);
            await connection.ExecuteAsync(sync);
        }

        await connection.ExecuteAsync(new CommandDefinition("UPDATE Authors SET DisplayName = Email WHERE DisplayName = '' OR DisplayName IS NULL;", cancellationToken: cancellationToken));

        if (await IdentityHelper.ColumnExistsAsync(connection, "Authors", "CreatedAtUtc", cancellationToken))
        {
            const string updateSql = "UPDATE Authors SET CreatedAtUtc = COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) WHERE CreatedAtUtc IS NULL;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, cancellationToken: cancellationToken));
        }

        if (await IdentityHelper.ColumnExistsAsync(connection, "Authors", "IsDeleted", cancellationToken))
        {
            const string resetSql = "UPDATE Authors SET IsDeleted = 0 WHERE IsDeleted IS NULL;";
            await connection.ExecuteAsync(new CommandDefinition(resetSql, cancellationToken: cancellationToken));
        }
    }

    private static async Task NormalizeAgentDataAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (await IdentityHelper.ColumnExistsAsync(connection, "Agents", "Username", cancellationToken))
        {
            var sync = new CommandDefinition("UPDATE Agents SET Username = Email WHERE Username IS NULL OR Username = '';", cancellationToken: cancellationToken);
            await connection.ExecuteAsync(sync);
        }

        await connection.ExecuteAsync(new CommandDefinition("UPDATE Agents SET DisplayName = COALESCE(NULLIF(DisplayName, ''), Email);", cancellationToken: cancellationToken));

        if (await IdentityHelper.ColumnExistsAsync(connection, "Agents", "CreatedAtUtc", cancellationToken))
        {
            const string updateSql = "UPDATE Agents SET CreatedAtUtc = COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) WHERE CreatedAtUtc IS NULL;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, cancellationToken: cancellationToken));
        }
    }

    private static async Task NormalizeBindingDataAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (await IdentityHelper.ColumnExistsAsync(connection, "Bindings", "CreatedAtUtc", cancellationToken))
        {
            const string updateSql = "UPDATE Bindings SET CreatedAtUtc = COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) WHERE CreatedAtUtc IS NULL;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, cancellationToken: cancellationToken));
        }
    }

    private static async Task BackfillAuthorSoftwaresAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string authorsSql = @"SELECT CAST(Id AS SIGNED) AS Id, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode,
    COALESCE(CreatedAtUtc, CreatedAt, UTC_TIMESTAMP()) AS CreatedAt
FROM Authors
WHERE (SoftwareCode IS NOT NULL AND SoftwareCode <> '') AND (IsDeleted = 0 OR IsDeleted IS NULL);";

        var authors = await connection.QueryAsync(new CommandDefinition(authorsSql, cancellationToken: cancellationToken));

        foreach (var author in authors)
        {
            var authorId = (int)author.Id;
            const string existsSql = "SELECT COUNT(*) FROM AuthorSoftwares WHERE AuthorId = @AuthorId";
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(existsSql, new { AuthorId = authorId }, cancellationToken: cancellationToken));
            if (exists > 0)
            {
                continue;
            }

            const string insertSql = @"INSERT INTO AuthorSoftwares (AuthorId, DisplayName, ApiAddress, ApiPort, SoftwareType, SoftwareCode, CreatedAt, CreatedAtUtc)
VALUES (@AuthorId, @DisplayName, @ApiAddress, @ApiPort, @SoftwareType, @SoftwareCode, @CreatedAt, @CreatedAtUtc);";

            try
            {
                await connection.ExecuteAsync(new CommandDefinition(insertSql, new
                {
                    AuthorId = authorId,
                    DisplayName = (string)author.DisplayName,
                    ApiAddress = (string)author.ApiAddress,
                    ApiPort = (int)author.ApiPort,
                    SoftwareType = (string)author.SoftwareType,
                    SoftwareCode = (string)author.SoftwareCode,
                    CreatedAt = (DateTime)author.CreatedAt,
                    CreatedAtUtc = (DateTime)author.CreatedAt
                }, cancellationToken: cancellationToken));
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Duplicate software codes will be ignored; existing records take precedence.
            }
        }
    }

    private static async Task BackfillBindingSoftwareDataAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (!await IdentityHelper.ColumnExistsAsync(connection, "Bindings", "SoftwareCode", cancellationToken))
        {
            return;
        }

        const string syncFromAuthors = @"UPDATE Bindings b
INNER JOIN Authors a ON a.Id = b.AuthorId
LEFT JOIN AuthorSoftwares s ON s.AuthorId = a.Id AND s.SoftwareCode = a.SoftwareCode
SET b.SoftwareCode = COALESCE(b.SoftwareCode, a.SoftwareCode),
    b.AuthorSoftwareId = COALESCE(b.AuthorSoftwareId, s.Id)
WHERE b.SoftwareCode IS NULL OR b.SoftwareCode = '' OR b.AuthorSoftwareId IS NULL OR b.AuthorSoftwareId = 0;";

        await connection.ExecuteAsync(new CommandDefinition(syncFromAuthors, cancellationToken: cancellationToken));

        const string syncMissing = @"UPDATE Bindings b
LEFT JOIN AuthorSoftwares s ON s.AuthorId = b.AuthorId AND (s.Id = b.AuthorSoftwareId OR b.AuthorSoftwareId IS NULL)
SET b.AuthorSoftwareId = s.Id,
    b.SoftwareCode = COALESCE(b.SoftwareCode, s.SoftwareCode)
WHERE s.Id IS NOT NULL AND (b.AuthorSoftwareId IS NULL OR b.AuthorSoftwareId = 0 OR b.SoftwareCode IS NULL OR b.SoftwareCode = '');";

        await connection.ExecuteAsync(new CommandDefinition(syncMissing, cancellationToken: cancellationToken));
    }

    private async Task EnsureIndexAsync(IDbConnection connection, string tableName, string indexName, IReadOnlyList<string> columns, bool unique, CancellationToken cancellationToken)
    {
        const string lookupSql = @"SELECT NON_UNIQUE, COLUMN_NAME
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND INDEX_NAME = @Index
ORDER BY SEQ_IN_INDEX";

        var existingRows = (await connection.QueryAsync<IndexMetadata>(new CommandDefinition(lookupSql, new
        {
            Table = tableName,
            Index = indexName
        }, cancellationToken: cancellationToken))).ToList();

        //if (existingRows.Count > 0)
        //{
        //    var isUnique = existingRows[0].NonUnique == 0;
        //    var existingColumns = existingRows.Select(row => row.ColumnName).ToArray();
        //    if (isUnique == unique && existingColumns.SequenceEqual(columns, StringComparer.OrdinalIgnoreCase))
        //    {
        //        return;
        //    }

        //    if (string.Equals(tableName, "AuthorSoftwares", StringComparison.OrdinalIgnoreCase)
        //        && string.Equals(indexName, "IDX_AuthorSoftwares_AuthorId", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _logger.LogInformation("检测到 {Table} 的索引 {Index} 已由管理员手动维护，跳过自动修复。", tableName, indexName);
        //        return;
        //    }

        //    if (string.Equals(tableName, "Bindings", StringComparison.OrdinalIgnoreCase)
        //        && string.Equals(indexName, "UX_Bindings", StringComparison.OrdinalIgnoreCase))
        //    {
        //        _logger.LogInformation("检测到 {Table} 的索引 {Index} 已由管理员手动维护，跳过自动修复。", tableName, indexName);
        //        return;
        //    }

        //    if (!await DropIndexAsync(connection, tableName, indexName, cancellationToken))
        //    {
        //        return;
        //    }
        //}

        var formattedColumns = string.Join(", ", columns.Select(column => $"`{column}`"));
        var createSql = unique
            ? $"CREATE UNIQUE INDEX `{indexName}` ON `{tableName}` ({formattedColumns})"
            : $"CREATE INDEX `{indexName}` ON `{tableName}` ({formattedColumns})";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(createSql, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法为 {Table} 创建索引 {Index}，将继续使用兼容模式。", tableName, indexName);
        }
    }

    private async Task ExecuteCreateTableAsync(IDbConnection connection, string tableName, string createSql, string? fallbackSql, CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(createSql, cancellationToken: cancellationToken));
        }
        catch (MySqlException ex) when ((ex.Number == 1215 || ex.Number == 1005) && !string.IsNullOrWhiteSpace(fallbackSql))
        {
            _logger.LogWarning(ex, "无法为 {Table} 应用外键约束，正在使用兼容模式创建表。", tableName);
            await connection.ExecuteAsync(new CommandDefinition(fallbackSql!, cancellationToken: cancellationToken));
        }
    }

    private async Task<bool> DropIndexAsync(IDbConnection connection, string tableName, string indexName, CancellationToken cancellationToken)
    {
        var dropSql = $"ALTER TABLE `{tableName}` DROP INDEX `{indexName}`";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(dropSql, cancellationToken: cancellationToken));
            return true;
        }
        catch (MySqlException ex) when (ex.Number == 1553)
        {
            if (string.Equals(tableName, "Bindings", StringComparison.OrdinalIgnoreCase)
                && string.Equals(indexName, "UX_Bindings", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex,
                    "无法自动删除 {Table} 上的索引 {Index}，请在数据库中执行 docs/upgrade-binding-index.sql 或按下列 SQL 手动处理后重新启动：\n" +
                    "ALTER TABLE `Bindings` DROP FOREIGN KEY `FK_Bindings_Agents`;\n" +
                    "ALTER TABLE `Bindings` DROP FOREIGN KEY `FK_Bindings_AuthorSoftwares`;\n" +
                    "ALTER TABLE `Bindings` DROP INDEX `UX_Bindings`;\n" +
                    "ALTER TABLE `Bindings` ADD UNIQUE INDEX `UX_Bindings` (`AgentId`,`AuthorSoftwareId`,`SoftwareCode`);\n" +
                    "ALTER TABLE `Bindings` ADD CONSTRAINT `FK_Bindings_Agents` FOREIGN KEY (`AgentId`) REFERENCES `Agents`(`Id`) ON DELETE CASCADE;\n" +
                    "ALTER TABLE `Bindings` ADD CONSTRAINT `FK_Bindings_AuthorSoftwares` FOREIGN KEY (`AuthorSoftwareId`) REFERENCES `AuthorSoftwares`(`Id`) ON DELETE CASCADE;",
                    tableName, indexName);
            }
            else
            {
                _logger.LogWarning(ex, "索引 {Index} 被外键引用，无法自动重新创建。", indexName);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法删除 {Table} 上的索引 {Index} 以重新创建，跳过索引修复。", tableName, indexName);
            return false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class ColumnMetadata
    {
        public string ColumnType { get; set; } = string.Empty;
        public string? ColumnDefault { get; set; }
        public string IsNullable { get; set; } = "YES";
        public string? Extra { get; set; }
    }

    private sealed class IndexMetadata
    {
        public int NonUnique { get; set; }
        public string ColumnName { get; set; } = string.Empty;
    }
}
