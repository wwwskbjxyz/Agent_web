using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Utilities;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public class SoftwareService
{
    private readonly DatabaseManager _databaseManager;
    private readonly PermissionHelper _permissionHelper;
    private readonly ILogger<SoftwareService> _logger;

    public SoftwareService(DatabaseManager databaseManager, PermissionHelper permissionHelper, ILogger<SoftwareService> logger)
    {
        _databaseManager = databaseManager;
        _permissionHelper = permissionHelper;
        _logger = logger;
    }

    public async Task<IList<SoftwareAgentInfo>> GetAccessibleSoftwareAsync(UserSession session)
    {
        if (session.SoftwareList.Count == 0)
        {
            return new List<SoftwareAgentInfo>();
        }

        var metadata = await LoadSoftwareMetadataAsync();
        var result = new List<SoftwareAgentInfo>();

        foreach (var software in session.SoftwareList)
        {
            session.SoftwareAgentInfo.TryGetValue(software, out var agent);

            var info = new SoftwareAgentInfo
            {
                SoftwareName = software,
                Idc = metadata.TryGetValue(software, out var meta) ? meta.Idc : null,
                State = metadata.TryGetValue(software, out meta) ? meta.State : 1,
                AgentInfo = BuildSoftwareAgent(agent),
                Permissions = BuildPermissionMap(agent),
            };

            result.Add(info);
        }

        return result;
    }

    public async Task<SoftwareAgentInfo?> GetSoftwareInfoAsync(string software, UserSession session)
    {
        if (!session.SoftwareAgentInfo.TryGetValue(software, out var agent))
        {
            return null;
        }

        var metadata = await LoadSoftwareMetadataAsync();
        metadata.TryGetValue(software, out var meta);

        return new SoftwareAgentInfo
        {
            SoftwareName = software,
            Idc = meta?.Idc,
            State = meta?.State ?? 1,
            AgentInfo = BuildSoftwareAgent(agent),
            Permissions = BuildPermissionMap(agent),
        };
    }

    private async Task<Dictionary<string, MultiSoftware>> LoadSoftwareMetadataAsync()
    {
        try
        {
            var databasePath = await _databaseManager.PrepareDatabasePathAsync("默认软件").ConfigureAwait(false);
            var records = await Task.Run(() => SqliteBridge.GetMultiSoftwareRecords(databasePath)).ConfigureAwait(false);

            return records
                .Select(record => new MultiSoftware
                {
                    SoftwareName = record.SoftwareName,
                    State = record.State,
                    Idc = string.IsNullOrWhiteSpace(record.Idc) ? null : record.Idc,
                    Version = record.Version
                })
                .ToDictionary(row => row.SoftwareName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load software metadata");
            return new Dictionary<string, MultiSoftware>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private SoftwareAgent? BuildSoftwareAgent(Agent? agent)
    {
        if (agent == null)
        {
            return null;
        }

        return new SoftwareAgent
        {
            Username = agent.User,
            Balance = agent.AccountBalance,
            TimeStock = agent.AccountTime,
            CardTypes = agent.CardTypeAuthNameArray.ToList(),
            Status = agent.Stat == 0 && agent.Deltm == 0 && !agent.IsExpired() ? "active" : "inactive",
            Expiration = agent.Duration,
            Permissions = BuildPermissionMap(agent),
        };
    }

    private IDictionary<string, bool> BuildPermissionMap(Agent? agent)
    {
        if (agent == null)
        {
            return new Dictionary<string, bool>();
        }

        var authority = _permissionHelper.ParseAuthority(agent.Authority);
        var result = new Dictionary<string, bool>();
        foreach (var kv in PermissionHelper.PermissionNames)
        {
            result[kv.Value] = (authority & kv.Key) != 0;
        }

        return result;
    }
}