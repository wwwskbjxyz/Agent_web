using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Dtos;

namespace SProtectAgentWeb.Api.Services;

/// <summary>Manages active heartbeat registrations reported by remote agents.</summary>
public class HeartbeatRegistry
{
    private readonly ConcurrentDictionary<string, HeartbeatSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _expiration;
    private readonly string _secret;
    private readonly ILogger<HeartbeatRegistry> _logger;

    public HeartbeatRegistry(IOptions<AppConfig> config, ILogger<HeartbeatRegistry> logger)
    {
        _logger = logger;
        var heartbeatConfig = config.Value.Heartbeat;
        _secret = heartbeatConfig.Secret?.Trim() ?? string.Empty;
        _expiration = TimeSpan.FromSeconds(Math.Clamp(heartbeatConfig.ExpirationSeconds, 30, 3600));
    }

    /// <summary>Records a heartbeat from the specified agent.</summary>
    public HeartbeatStatus RecordHeartbeat(HeartbeatRequest request)
    {
        var receivedAt = DateTimeOffset.UtcNow;

        if (request is null)
        {
            return new HeartbeatStatus
            {
                Accepted = false,
                Message = "请求不能为空",
                ReceivedAt = receivedAt
            };
        }

        if (string.IsNullOrWhiteSpace(request.Service))
        {
            return new HeartbeatStatus
            {
                Accepted = false,
                Message = "服务名称不能为空",
                ReceivedAt = receivedAt
            };
        }

        if (!string.IsNullOrEmpty(_secret)
            && !string.Equals(request.Secret?.Trim(), _secret, StringComparison.Ordinal))
        {
            _logger.LogWarning("Heartbeat rejected for {Service}/{Machine}: invalid secret", request.Service, request.Machine);
            return new HeartbeatStatus
            {
                Accepted = false,
                Message = "身份验证失败",
                ReceivedAt = receivedAt
            };
        }

        var service = request.Service.Trim();
        var machine = string.IsNullOrWhiteSpace(request.Machine)
            ? "unknown"
            : request.Machine.Trim();

        var snapshot = new HeartbeatSnapshot
        {
            Service = service,
            Machine = machine,
            LastSeen = receivedAt,
            LatencyMilliseconds = CalculateLatency(receivedAt, request.Timestamp)
        };

        var key = BuildKey(service, machine);
        _snapshots.AddOrUpdate(key, snapshot, (_, _) => snapshot);

        _logger.LogDebug("Heartbeat recorded for {Service}/{Machine}", service, machine);

        CleanupExpired(receivedAt);

        return new HeartbeatStatus
        {
            Accepted = true,
            Message = "心跳已记录",
            ReceivedAt = receivedAt
        };
    }

    /// <summary>Returns the current active heartbeat snapshots.</summary>
    public IReadOnlyCollection<HeartbeatSnapshot> GetSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        CleanupExpired(now);

        return _snapshots.Values
            .Where(snapshot => now - snapshot.LastSeen <= _expiration)
            .OrderBy(snapshot => snapshot.Service, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Machine, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void CleanupExpired(DateTimeOffset referenceTime)
    {
        foreach (var (key, snapshot) in _snapshots)
        {
            if (referenceTime - snapshot.LastSeen > _expiration)
            {
                _snapshots.TryRemove(key, out _);
            }
        }
    }

    private static string BuildKey(string service, string machine)
        => $"{service}\u001f{machine}";

    private static long CalculateLatency(DateTimeOffset receivedAt, long timestamp)
    {
        if (timestamp <= 0)
        {
            return -1;
        }

        try
        {
            var sentAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            return (long)Math.Abs((receivedAt - sentAt).TotalMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return -1;
        }
    }
}
