using System.Linq;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Services;

public class CardTypeService
{
    private readonly DatabaseManager _databaseManager;
    private readonly ILogger<CardTypeService> _logger;

    public CardTypeService(DatabaseManager databaseManager, ILogger<CardTypeService> logger)
    {
        _databaseManager = databaseManager;
        _logger = logger;
    }

    public async Task<IList<CardType>> GetCardTypesAsync(string software)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var records = await Task.Run(() => SqliteBridge.GetCardTypes(dbPath)).ConfigureAwait(false);
        return records.Select(MapCardType).ToList();
    }

    public async Task<CardType?> GetCardTypeByNameAsync(string software, string name)
    {
        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var record = await Task.Run(() => SqliteBridge.GetCardTypeByName(dbPath, name)).ConfigureAwait(false);
        return record.HasValue ? MapCardType(record.Value) : null;
    }

    private static CardType MapCardType(SqliteBridge.CardTypeRecord record)
    {
        return new CardType
        {
            Name = record.Name,
            Prefix = record.Prefix,
            Duration = record.Duration,
            FYI = record.Fyi,
            Price = record.Price,
            Param = record.Param,
            Bind = record.Bind,
            OpenNum = record.OpenNum,
            Remarks = record.Remarks,
            CannotBeChanged = record.CannotBeChanged != 0,
            Attr_UnBindLimitTime = record.AttrUnbindLimitTime,
            Attr_UnBindDeductTime = record.AttrUnbindDeductTime,
            Attr_UnBindFreeCount = record.AttrUnbindFreeCount,
            Attr_UnBindMaxCount = record.AttrUnbindMaxCount,
            BindIP = record.BindIp,
            BindMachineNum = record.BindMachineNum,
            LockBindPcsign = record.LockBindPcsign,
            ActivateTime_ = record.ActivateTime,
            ExpiredTime_ = record.ExpiredTime,
            LastLoginTime_ = record.LastLoginTime,
            Delstate = record.DelState,
            Cty = record.Cty != 0,
            ExpiredTime__ = record.ExpiredTime2,
        };
    }
}
