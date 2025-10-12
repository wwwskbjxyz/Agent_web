using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SProtectAgentWeb.Api.Services;

public sealed class ClientIpResolver
{
    private static readonly string[] ForwardedHeaders =
    {
        "CF-Connecting-IP",
        "X-Forwarded-For",
        "X-Real-IP",
        "True-Client-IP",
        "X-Client-IP",
        "X-Original-Forwarded-For"
    };

    private static readonly Uri IpEchoEndpoint = new("https://api.ipify.org?format=text");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ClientIpResolver> _logger;

    public ClientIpResolver(IHttpClientFactory httpClientFactory, ILogger<ClientIpResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var headerIp = TryResolveFromHeaders(context);
        if (!string.IsNullOrWhiteSpace(headerIp))
        {
            return headerIp;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null && !IsPrivate(remoteIp))
        {
            return remoteIp.ToString();
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(ClientIpResolver));
            client.Timeout = TimeSpan.FromSeconds(5);

            using var request = new HttpRequestMessage(HttpMethod.Get, IpEchoEndpoint);
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("IP echo endpoint returned status {StatusCode}", response.StatusCode);
                return remoteIp?.ToString() ?? "unknown";
            }

            var payload = (await response.Content.ReadAsStringAsync()).Trim();
            if (TryNormalizeIp(payload, out var normalized))
            {
                return normalized;
            }

            _logger.LogWarning("IP echo endpoint returned an unexpected payload: {Payload}", payload);
            return remoteIp?.ToString() ?? "unknown";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to resolve client IP via third-party endpoint");
            return remoteIp?.ToString() ?? "unknown";
        }
    }

    private static string? TryResolveFromHeaders(HttpContext context)
    {
        foreach (var header in ForwardedHeaders)
        {
            if (!context.Request.Headers.TryGetValue(header, out var values))
            {
                continue;
            }

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var candidates = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var candidate in candidates)
                {
                    if (TryNormalizeIp(candidate, out var normalized))
                    {
                        return normalized;
                    }
                }
            }
        }

        return null;
    }

    private static bool TryNormalizeIp(string candidate, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!IPAddress.TryParse(candidate.Trim(), out var address))
        {
            return false;
        }

        if (IsPrivate(address))
        {
            return false;
        }

        normalized = address.ToString();
        return true;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                169 when bytes[1] == 254 => true,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6Teredo ||
                   address.IsIPv6UniqueLocal;
        }

        return false;
    }
}
