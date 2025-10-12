using System;
using System.Collections.Generic;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>
/// 表示服务器系统状态信息。
/// </summary>
public class SystemStatusResponse
{
    public string MachineName { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public DateTimeOffset ServerTime { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? BootTime { get; set; }
    public double? CpuLoadPercentage { get; set; }
    public ulong? TotalMemoryBytes { get; set; }
    public ulong? FreeMemoryBytes { get; set; }
    public ulong? UsedMemoryBytes { get; set; }
    public double? MemoryUsagePercentage { get; set; }
    public double? UptimeSeconds { get; set; }
    public IList<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// 公告内容响应。
/// </summary>
public class AnnouncementResponse
{
    public string Content { get; set; } = string.Empty;
    public long UpdatedAt { get; set; }
}
