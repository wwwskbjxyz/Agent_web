using System;

namespace SProtectAgentWeb.Api.Models;

public sealed class SettlementProfile
{
    public long Id { get; set; }

    public string Software { get; set; } = string.Empty;

    public string AgentUsername { get; set; } = string.Empty;

    public int CycleDays { get; set; }

    public int CycleTimeMinutes { get; set; }

    public DateTime? LastSettledAtUtc { get; set; }

    public DateTime? NextDueAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
