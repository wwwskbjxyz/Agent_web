using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SProtectAgentWeb.Api.Configuration;

namespace SProtectAgentWeb.Api.Utilities;

public sealed class PlatformSignatureValidationMiddleware
{
    private const string TimestampHeader = "X-SProtect-Timestamp";
    private const string ContentHashHeader = "X-SProtect-Content-Hash";
    private const string SignatureHeader = "X-SProtect-Signature";
    private const string AlgorithmHeader = "X-SProtect-Signature-Algorithm";
    private const string ExpectedAlgorithm = "HMAC-SHA256";

    private readonly RequestDelegate _next;
    private readonly AppConfig _config;
    public PlatformSignatureValidationMiddleware(
        RequestDelegate next,
        AppConfig config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var integration = _config.PlatformIntegration ?? new PlatformIntegrationConfig();

        if (!ShouldValidate(context, integration))
        {
            await _next(context);
            return;
        }

        var sharedSecret = integration.SharedSecret?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "作者端未配置平台对接密钥"));
            return;
        }

        if (!TryGetHeader(context, TimestampHeader, out var timestampValue) ||
            !TryGetHeader(context, ContentHashHeader, out var providedContentHash) ||
            !TryGetHeader(context, SignatureHeader, out var providedSignature))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "平台签名缺失"));
            return;
        }

        if (context.Request.Headers.TryGetValue(AlgorithmHeader, out var algorithmValues))
        {
            var algorithm = algorithmValues.ToString();
            if (!string.Equals(algorithm, ExpectedAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "不支持的签名算法"));
                return;
            }
        }

        if (!long.TryParse(timestampValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "签名时间戳无效"));
            return;
        }

        var allowedSkew = Math.Clamp(integration.AllowedClockSkewSeconds, 0, 900);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestampSeconds) > allowedSkew)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "签名已过期"));
            return;
        }

        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync() ?? string.Empty;
        }
        context.Request.Body.Position = 0;

        var computedContentHash = ComputeSha256(requestBody);
        if (!FixedTimeEquals(computedContentHash, providedContentHash))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "请求内容已被篡改"));
            return;
        }

        var method = context.Request.Method?.ToUpperInvariant() ?? HttpMethods.Get;
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        var pathAndQuery = context.Request.QueryString.HasValue
            ? path + context.Request.QueryString.Value
            : path;
        var canonicalRequest = string.Join('\n', new[] { method, pathAndQuery, timestampValue, computedContentHash });
        var expectedSignature = ComputeSignature(sharedSecret, canonicalRequest);

        if (!FixedTimeEquals(expectedSignature, providedSignature))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(ErrorCodes.InvalidRequest, "签名校验失败"));
            return;
        }

        await _next(context);
    }

    private static bool ShouldValidate(HttpContext context, PlatformIntegrationConfig integration)
    {
        var headers = context.Request.Headers;
        var hasPlatformHeaders = headers.ContainsKey("X-SProtect-Author-Account") || headers.ContainsKey("X-SProtect-Author-Password");
        if (!hasPlatformHeaders)
        {
            return false;
        }

        var bypassPaths = integration.SignatureBypassPaths;
        if (bypassPaths == null || bypassPaths.Count == 0)
        {
            return true;
        }

        var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        return !IsBypassedPath(requestPath, bypassPaths);
    }

    private static bool IsBypassedPath(string actualPath, IList<string> bypassPaths)
    {
        for (var i = 0; i < bypassPaths.Count; i++)
        {
            if (MatchesPath(actualPath, bypassPaths[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPath(string actualPath, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var trimmedPattern = pattern.Trim();
        var hasWildcard = trimmedPattern.EndsWith("/*", StringComparison.Ordinal);
        if (hasWildcard)
        {
            trimmedPattern = trimmedPattern[..^2];
        }

        var normalizedPattern = NormalizePath(trimmedPattern);
        var normalizedActual = NormalizePath(actualPath);

        if (hasWildcard)
        {
            if (normalizedActual.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase) &&
                (normalizedActual.Length == normalizedPattern.Length || normalizedActual[normalizedPattern.Length] == '/'))
            {
                return true;
            }

            return NormalizeForComparison(normalizedActual).StartsWith(
                NormalizeForComparison(normalizedPattern),
                StringComparison.Ordinal);
        }

        return string.Equals(normalizedActual, normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   NormalizeForComparison(normalizedActual),
                   NormalizeForComparison(normalizedPattern),
                   StringComparison.Ordinal);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        var queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized.TrimStart('/');
        }

        while (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '-' or '_':
                    continue;
                case '/':
                    builder.Append('/');
                    break;
                default:
                    builder.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryGetHeader(HttpContext context, string headerName, out string value)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var values))
        {
            value = values.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
