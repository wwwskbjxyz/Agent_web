using System;

namespace SProtectAgentWeb.Api.Models;

public sealed class SettlementBill
{
    public long Id { get; set; }

    public string Software { get; set; } = string.Empty;

    public string AgentUsername { get; set; } = string.Empty;

    public DateTime CycleStartUtc { get; set; }

    public DateTime CycleEndUtc { get; set; }

    public decimal Amount { get; set; }

    public int Status { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SettledAtUtc { get; set; }

    public bool IsSettled => Status > 0;
}
