using System;
using System.Data;
using Dapper;
using System.Threading;
using System.Threading.Tasks;

namespace SProtectPlatform.Api.Data;

internal static class IdentityHelper
{
    private const string ColumnExistsSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND LOWER(COLUMN_NAME) = LOWER(@Column);";

    public static async Task<bool> ColumnHasAutoIncrementAsync(IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT EXTRA FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = LOWER(@Table) AND LOWER(COLUMN_NAME) = 'id';";

        var extra = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new
        {
            Table = tableName
        }, cancellationToken: cancellationToken));

        return extra is not null && extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<long> GetNextIdAsync(IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var sql = $"SELECT COALESCE(MAX(CAST(Id AS SIGNED)), 0) + 1 FROM `{tableName}`";
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<long>(command);
    }

    public static async Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(ColumnExistsSql, new
        {
            Table = tableName,
            Column = columnName
        }, cancellationToken: cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(command);
        return count > 0;
    }
}
