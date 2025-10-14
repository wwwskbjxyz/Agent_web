using System;

namespace SProtectAgentWeb.Api.Models;

public sealed class SettlementCycleInfo
{
    public string AgentUsername { get; set; } = string.Empty;

    public int OwnCycleDays { get; set; }

    public int EffectiveCycleDays { get; set; }

    public int OwnCycleTimeMinutes { get; set; }

    public int EffectiveCycleTimeMinutes { get; set; }

    public bool IsInherited { get; set; }

    public DateTime? LastSettledAtUtc { get; set; }

    public DateTime? NextDueAtUtc { get; set; }

    public bool IsDue => EffectiveCycleDays > 0 && NextDueAtUtc.HasValue && NextDueAtUtc.Value <= DateTime.UtcNow;
}
