using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Models;

namespace SProtectPlatform.Api.Services;

public interface IWeChatBindingService
{
    Task<WeChatBinding?> GetBindingAsync(string userType, int userId, CancellationToken cancellationToken);
    Task<WeChatBinding?> GetBindingByOpenIdAsync(string userType, string openId, CancellationToken cancellationToken);
    Task<WeChatBinding> UpsertBindingAsync(string userType, int userId, string openId, string? unionId, string? nickname, CancellationToken cancellationToken);
    Task RemoveBindingAsync(string userType, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WeChatBinding>> GetBindingsAsync(string userType, IEnumerable<int> userIds, CancellationToken cancellationToken);
}

public sealed class WeChatBindingService : IWeChatBindingService
{
    private readonly IMySqlConnectionFactory _connectionFactory;
    public WeChatBindingService(IMySqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WeChatBinding?> GetBindingAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, UserType, UserId, OpenId, UnionId, Nickname, CreatedAtUtc, UpdatedAtUtc
FROM WeChatBindings WHERE UserType = @UserType AND UserId = @UserId LIMIT 1;";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<WeChatBinding>(new CommandDefinition(sql, new { UserType = userType, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<WeChatBinding?> GetBindingByOpenIdAsync(string userType, string openId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, UserType, UserId, OpenId, UnionId, Nickname, CreatedAtUtc, UpdatedAtUtc
FROM WeChatBindings WHERE UserType = @UserType AND OpenId = @OpenId LIMIT 1;";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<WeChatBinding>(new CommandDefinition(sql, new { UserType = userType, OpenId = openId }, cancellationToken: cancellationToken));
    }

    public async Task<WeChatBinding> UpsertBindingAsync(string userType, int userId, string openId, string? unionId, string? nickname, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO WeChatBindings (UserType, UserId, OpenId, UnionId, Nickname)
VALUES (@UserType, @UserId, @OpenId, @UnionId, @Nickname)
ON DUPLICATE KEY UPDATE OpenId = VALUES(OpenId), UnionId = VALUES(UnionId), Nickname = CASE WHEN VALUES(Nickname) IS NULL OR VALUES(Nickname) = '' THEN Nickname ELSE VALUES(Nickname) END, UpdatedAtUtc = CURRENT_TIMESTAMP();
SELECT Id, UserType, UserId, OpenId, UnionId, Nickname, CreatedAtUtc, UpdatedAtUtc FROM WeChatBindings WHERE UserType = @UserType AND UserId = @UserId LIMIT 1;";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstAsync<WeChatBinding>(new CommandDefinition(sql, new
        {
            UserType = userType,
            UserId = userId,
            OpenId = openId,
            UnionId = unionId,
            Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname
        }, cancellationToken: cancellationToken));
    }

    public async Task RemoveBindingAsync(string userType, int userId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM WeChatBindings WHERE UserType = @UserType AND UserId = @UserId;";
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UserType = userType, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<WeChatBinding>> GetBindingsAsync(string userType, IEnumerable<int> userIds, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, UserType, UserId, OpenId, UnionId, Nickname, CreatedAtUtc, UpdatedAtUtc
FROM WeChatBindings WHERE UserType = @UserType AND UserId IN @Ids";

        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var bindings = await connection.QueryAsync<WeChatBinding>(new CommandDefinition(sql, new { UserType = userType, Ids = userIds }, cancellationToken: cancellationToken));
        return bindings.AsList();
    }
}
