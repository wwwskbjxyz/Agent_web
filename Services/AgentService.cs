using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Utilities;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public class AgentService
{
    private readonly DatabaseManager _databaseManager;
    private readonly PermissionHelper _permissionHelper;

    public AgentService(DatabaseManager databaseManager, PermissionHelper permissionHelper, ILogger<AgentService> logger)
    {
        _databaseManager = databaseManager;
        _permissionHelper = permissionHelper;
        ArgumentNullException.ThrowIfNull(logger);
    }

    public async Task<AgentInfoResponse> GetAgentInfoAsync(string software, string username)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var agentRecord = await Task.Run(() => SqliteBridge.GetAgent(dbPath, username)).ConfigureAwait(false);
        if (agentRecord is null)
        {
            throw new InvalidOperationException("代理不存在");
        }

        var agent = MapAgent(agentRecord.Value);

        agent.CardTypeAuthNameArray = _permissionHelper.ParseBracketList(agent.CardTypeAuthName).ToList();

        var permissions = _permissionHelper.GetPermissionStrings(agent.Authority).ToList();
        var statisticsRecord = await Task.Run(() => SqliteBridge.GetAgentStatistics(dbPath, username)).ConfigureAwait(false);
        var statistics = MapStatistics(statisticsRecord);

        return new AgentInfoResponse
        {
            Agent = agent,
            Permissions = permissions,
            Statistics = statistics,
        };
    }

    public async Task<IList<object>> GetSubAgentsAsync(SubAgentListRequest request, string parentUsername, bool includeAllDescendants)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(request.Software).ConfigureAwait(false);
        var agentRecords = await Task.Run(() => SqliteBridge.GetAgents(dbPath)).ConfigureAwait(false);
        var agents = agentRecords
            .Select(MapAgent)
            .Where(agent => agent.Deltm == 0)
            .ToList();

        var filtered = agents
            .Where(agent => includeAllDescendants
                ? agent.IsChildOf(_permissionHelper, parentUsername)
                : agent.IsDirectChildOf(_permissionHelper, parentUsername))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            filtered = request.SearchType switch
            {
                0 => filtered.Where(agent => string.Equals(agent.User, request.Keyword, StringComparison.OrdinalIgnoreCase)).ToList(),
                _ => filtered.Where(agent => agent.User.Contains(request.Keyword, StringComparison.OrdinalIgnoreCase)).ToList(),
            };
        }

        foreach (var agent in filtered)
        {
            agent.CardTypeAuthNameArray = _permissionHelper.ParseBracketList(agent.CardTypeAuthName).ToList();
        }

        var payload = filtered
            .Select(agent =>
            {
                var chain = _permissionHelper.ParseAgentFNode(agent.FNode).ToList();
                var parent = agent.GetParentAgent(_permissionHelper);
                return new
                {
                    username = agent.User,
                    password = agent.Password,
                    balance = agent.AccountBalance,
                    time_stock = agent.AccountTime,
                    parities = agent.Parities,
                    total_parities = agent.TatalParities,
                    status = agent.Stat,
                    expiration = agent.Duration_,
                    permissions = _permissionHelper.GetPermissionStrings(agent.Authority),
                    card_types = agent.CardTypeAuthNameArray,
                    parent,
                    hierarchy = chain,
                    depth = Math.Max(0, chain.Count - 1),
                    remark = agent.Remarks,
                };
            })
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit)
            .Cast<object>()
            .ToList();

        return payload;
    }

    public async Task<int> CountSubAgentsAsync(string software, string parentUsername, bool includeAllDescendants)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var agentRecords = await Task.Run(() => SqliteBridge.GetAgents(dbPath)).ConfigureAwait(false);
        return agentRecords
            .Select(MapAgent)
            .Where(agent => agent.Deltm == 0)
            .Count(agent => includeAllDescendants
            ? agent.IsChildOf(_permissionHelper, parentUsername)
            : agent.IsDirectChildOf(_permissionHelper, parentUsername));
    }

    public async Task EnableAgentsAsync(string software, IEnumerable<string> usernames)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.SetAgentStatuses(dbPath, usernames, enable: true)).ConfigureAwait(false);
    }

    public async Task DisableAgentsAsync(string software, IEnumerable<string> usernames)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.SetAgentStatuses(dbPath, usernames, enable: false)).ConfigureAwait(false);
    }

    public async Task UpdateRemarkAsync(string software, string username, string remark)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.UpdateAgentRemark(dbPath, username, remark)).ConfigureAwait(false);
    }

    public async Task CreateSubAgentAsync(string software, string parentUsername, CreateAgentRequest request)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var parentRecord = await Task.Run(() => SqliteBridge.GetAgent(dbPath, parentUsername)).ConfigureAwait(false);
        if (parentRecord is null)
        {
            throw new InvalidOperationException("父级代理不存在");
        }

        var parent = MapAgent(parentRecord.Value);

        var fnode = _permissionHelper.GenerateChildFNode(parent.FNode, request.Username);
        var cardTypeValue = _permissionHelper.BuildBracketList(request.CardTypes);

        await Task.Run(() => SqliteBridge.CreateAgent(
                dbPath,
                request.Username,
                request.Password,
                request.InitialBalance,
                request.InitialTimeStock,
                parent.Authority ?? string.Empty,
                cardTypeValue,
                request.Remark ?? string.Empty,
                fnode,
                request.Parities,
                request.TotalParities))
            .ConfigureAwait(false);
    }

    public async Task DeleteAgentsAsync(string software, IEnumerable<string> usernames)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.SoftDeleteAgents(dbPath, usernames)).ConfigureAwait(false);
    }

    public async Task UpdateAgentPasswordAsync(string software, string username, string newPassword)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.UpdateAgentPassword(dbPath, username, newPassword)).ConfigureAwait(false);
    }

    public async Task AddMoneyAsync(string software, string username, double balance, long timeStock)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        await Task.Run(() => SqliteBridge.AddAgentBalance(dbPath, username, balance, timeStock)).ConfigureAwait(false);
    }

    public async Task<IList<string>> GetAgentCardTypesAsync(string software, string username)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var agentRecord = await Task.Run(() => SqliteBridge.GetAgent(dbPath, username)).ConfigureAwait(false);
        if (agentRecord is null)
        {
            return new List<string>();
        }

        return _permissionHelper.ParseBracketList(agentRecord.Value.CardTypeAuthName).ToList();
    }

    public async Task SetAgentCardTypesAsync(string software, string username, IEnumerable<string> cardTypes)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var value = _permissionHelper.BuildBracketList(cardTypes);
        await Task.Run(() => SqliteBridge.SetAgentCardTypes(dbPath, username, value)).ConfigureAwait(false);
    }

    private static Agent MapAgent(SqliteBridge.AgentRecord record)
    {
        return new Agent
        {
            User = record.User,
            Password = record.Password,
            AccountBalance = record.AccountBalance,
            AccountTime = record.AccountTime,
            Duration = record.Duration,
            Authority = record.Authority,
            CardTypeAuthName = record.CardTypeAuthName,
            CardsEnable = record.CardsEnable != 0,
            Remarks = record.Remarks,
            FNode = record.FNode,
            Stat = record.Stat,
            Deltm = record.DeletedAt,
            Duration_ = record.DurationRaw,
            Parities = record.Parities,
            TatalParities = record.TotalParities,
        };
    }

    private static AgentStatistics MapStatistics(SqliteBridge.AgentStatisticsRecord record)
    {
        return new AgentStatistics
        {
            TotalCards = record.TotalCards,
            ActiveCards = record.ActiveCards,
            UsedCards = record.UsedCards,
            ExpiredCards = record.ExpiredCards,
            SubAgents = record.SubAgents,
        };
    }
}
