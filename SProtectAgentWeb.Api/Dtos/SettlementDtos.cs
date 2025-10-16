using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SProtectAgentWeb.Api.Dtos;

public class SettlementRateListRequest
{
    [Required]
    public string Software { get; set; } = string.Empty;

    /// <summary>
    /// 需要查询或编辑的目标代理，留空表示当前登录代理。
    /// </summary>
    public string? TargetAgent { get; set; }
}

public class SettlementRateDto
{
    public string CardType { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

public class SettlementRateListResponse
{
    public IList<SettlementRateDto> Rates { get; set; } = new List<SettlementRateDto>();

    /// <summary>
    /// 本次返回对应的代理用户名。
    /// </summary>
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// 可供选择的下级代理列表。
    /// </summary>
    public IList<SettlementAgentOption> Agents { get; set; } = new List<SettlementAgentOption>();

    /// <summary>
    /// 当前代理的结算周期信息。
    /// </summary>
    public SettlementCycleDto? Cycle { get; set; }

    /// <summary>
    /// 待结算账单列表（按时间倒序）。
    /// </summary>
    public IList<SettlementBillDto> Bills { get; set; } = new List<SettlementBillDto>();

    /// <summary>
    /// 是否存在待结算提醒，用于快捷入口展示红点。
    /// </summary>
    public bool HasPendingReminder { get; set; }
}

public class SettlementRateUpdateRequest
{
    [Required]
    public string Software { get; set; } = string.Empty;

    public IList<SettlementRateDto> Rates { get; set; } = new List<SettlementRateDto>();

    /// <summary>
    /// 可选的结算周期（单位：天），传空表示不修改。
    /// </summary>
    public int? CycleDays { get; set; }

    /// <summary>
    /// 可选的结算时间（单位：分钟，0-1439），传空表示不修改。
    /// </summary>
    public int? CycleTimeMinutes { get; set; }

    /// <summary>
    /// 需要更新的目标代理，留空表示当前登录代理。
    /// </summary>
    public string? TargetAgent { get; set; }
}

public class SettlementAgentOption
{
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 是否存在待结算提醒。
    /// </summary>
    public bool HasPendingReminder { get; set; }
}

public class SettlementCycleDto
{
    /// <summary>
    /// 当前生效的结算周期（天）。
    /// </summary>
    public int EffectiveDays { get; set; }

    /// <summary>
    /// 自身配置的结算周期（天），可能为 0 表示继承。
    /// </summary>
    public int OwnDays { get; set; }

    /// <summary>
    /// 当前生效的结算时间（分钟）。
    /// </summary>
    public int EffectiveTimeMinutes { get; set; }

    /// <summary>
    /// 自身配置的结算时间（分钟），可能为 0 表示凌晨 00:00。
    /// </summary>
    public int OwnTimeMinutes { get; set; }

    /// <summary>
    /// 当前生效的结算时间（可读字符串）。
    /// </summary>
    public string EffectiveTimeLabel { get; set; } = string.Empty;

    /// <summary>
    /// 自身配置的结算时间（可读字符串）。
    /// </summary>
    public string OwnTimeLabel { get; set; } = string.Empty;

    /// <summary>
    /// 是否继承自上级或全局设置。
    /// </summary>
    public bool IsInherited { get; set; }

    /// <summary>
    /// 下次结算时间（UTC）。
    /// </summary>
    public string? NextDueTimeUtc { get; set; }

    /// <summary>
    /// 最近一次结算时间（UTC）。
    /// </summary>
    public string? LastSettledTimeUtc { get; set; }

    /// <summary>
    /// 是否已经到达结算周期。
    /// </summary>
    public bool IsDue { get; set; }
}

public class SettlementBillDto
{
    public long Id { get; set; }

    public string CycleStartUtc { get; set; } = string.Empty;

    public string CycleEndUtc { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal? SuggestedAmount { get; set; }

    public bool IsSettled { get; set; }

    public string? SettledAtUtc { get; set; }

    public string? Note { get; set; }

    public IList<SettlementBillBreakdownDto> Breakdowns { get; set; } = new List<SettlementBillBreakdownDto>();
}

public class SettlementBillBreakdownDto
{
    public string Agent { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public long Count { get; set; }

    public decimal Amount { get; set; }
}

public class SettlementBillCompleteRequest
{
    [Required]
    public string Software { get; set; } = string.Empty;

    public string? TargetAgent { get; set; }

    [Required]
    public long BillId { get; set; }

    /// <summary>
    /// 本次结算金额。
    /// </summary>
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
