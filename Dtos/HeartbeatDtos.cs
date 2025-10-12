using System.ComponentModel.DataAnnotations;

namespace SProtectAgentWeb.Api.Dtos;

/// <summary>守护进程心跳请求。</summary>
public class HeartbeatRequest
{
    /// <summary>守护进程名称。</summary>
    [Required]
    [MaxLength(128)]
    public string Service { get; set; } = string.Empty;

    /// <summary>机器名。</summary>
    [MaxLength(128)]
    public string Machine { get; set; } = string.Empty;

    /// <summary>客户端时间戳，Unix秒。</summary>
    public long Timestamp { get; set; }
        = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>可选的共享密钥。</summary>
    [MaxLength(256)]
    public string? Secret { get; set; }
        = string.Empty;
}

/// <summary>守护进程心跳响应。</summary>
public class HeartbeatStatus
{
    public bool Accepted { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>守护进程最新状态。</summary>
public class HeartbeatSnapshot
{
    public string Service { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public DateTimeOffset LastSeen { get; set; }
        = DateTimeOffset.MinValue;
    public long LatencyMilliseconds { get; set; }
        = -1;
}
