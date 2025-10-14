using System.Data;
using Microsoft.Extensions.Options;
using MySqlConnector;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Data;

public interface IMySqlConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class MySqlConnectionFactory : IMySqlConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IOptions<MySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString ?? string.Empty;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("MySQL connection string is not configured.");
        }

        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
