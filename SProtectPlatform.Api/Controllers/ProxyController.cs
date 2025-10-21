using System;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Options;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Agent)]
[Route("api/proxy/{softwareCode}")]
public sealed class ProxyController : ControllerBase
{
    private static readonly string[] HopByHopHeaders =
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailers",
        "Transfer-Encoding",
        "Upgrade"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBindingService _bindingService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ForwardingOptions _forwardingOptions;

    public ProxyController(
        IHttpClientFactory httpClientFactory,
        IBindingService bindingService,
        ICredentialProtector credentialProtector,
        IOptions<ForwardingOptions> forwardingOptions)
    {
        _httpClientFactory = httpClientFactory;
        _bindingService = bindingService;
        _credentialProtector = credentialProtector;
        _forwardingOptions = forwardingOptions.Value;
    }

    [HttpGet("{**path}")]
    [HttpPost("{**path}")]
    [HttpPut("{**path}")]
    [HttpDelete("{**path}")]
    [HttpPatch("{**path}")]
    [HttpOptions("{**path}")]
    [HttpHead("{**path}")]
    [AllowAnonymous]
    public async Task<IActionResult> ForwardAsync(string softwareCode, string? path, CancellationToken cancellationToken)
    {
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        bool attemptedAgentLookup = false;
        BindingRecord? binding = null;

        if (isAuthenticated)
        {
            try
            {
                var agentId = User.GetAgentId();
                binding = await _bindingService.GetBindingAsync(agentId, softwareCode, cancellationToken);
                attemptedAgentLookup = true;
            }
            catch
            {
                attemptedAgentLookup = false;
            }
        }

        if (binding == null && !attemptedAgentLookup)
        {
            binding = await _bindingService.GetBindingBySoftwareCodeAsync(softwareCode, cancellationToken);
        }
        if (binding == null)
        {
            return NotFound(ApiResponse<string>.Failure("未绑定该软件码", 404));
        }

        var authorPassword = _credentialProtector.Unprotect(binding.EncryptedAuthorPassword);
        var remoteToken = Request.Headers["X-SProtect-Remote-Token"].FirstOrDefault();

        Request.EnableBuffering();

        var uriBuilder = new UriBuilder
        {
            Scheme = Uri.UriSchemeHttp,
            Host = binding.ApiAddress,
            Port = binding.ApiPort,
            Path = CombinePaths(path),
            Query = Request.QueryString.HasValue ? Request.QueryString.Value : null
        };

        var targetUri = uriBuilder.Uri;
        var requestMessage = new HttpRequestMessage(new HttpMethod(Request.Method), targetUri);

        string requestBody = string.Empty;

        if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync(cancellationToken) ?? string.Empty;
            Request.Body.Position = 0;
            requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, Request.ContentType ?? "application/json");
        }

        foreach (var header in Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "X-SProtect-Remote-Token", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        requestMessage.Headers.Host = targetUri.Host;
        requestMessage.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse($"SProtectPlatform/1.0"));
        requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Author-Account", binding.AuthorAccount);
        requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Author-Password", authorPassword);

        var requiresSignature = RequiresSignature(targetUri);
        if (requiresSignature)
        {
            var sharedSecret = _forwardingOptions.SharedSecret?.Trim();
            if (string.IsNullOrEmpty(sharedSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<string>.Failure("平台未配置作者端对接密钥", StatusCodes.Status503ServiceUnavailable));
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var method = Request.Method?.ToUpperInvariant() ?? HttpMethods.Get;
            var contentHash = ComputeSha256(requestBody);
            var pathAndQuery = targetUri.PathAndQuery;
            var canonicalRequest = string.Join('\n', new[] { method, pathAndQuery, timestamp, contentHash });
            var signature = ComputeSignature(sharedSecret, canonicalRequest);

            requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Timestamp", timestamp);
            requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Content-Hash", contentHash);
            requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Signature", signature);
            requestMessage.Headers.TryAddWithoutValidation("X-SProtect-Signature-Algorithm", "HMAC-SHA256");
        }

        if (!string.IsNullOrWhiteSpace(remoteToken))
        {
            var trimmed = remoteToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? remoteToken.Substring("Bearer ".Length)
                : remoteToken;
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", trimmed);
            }
        }

        var client = _httpClientFactory.CreateClient(nameof(ProxyController));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_forwardingOptions.RequestTimeoutSeconds, 5, 120));

        using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        foreach (var header in responseMessage.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        if (responseMessage.Content != null)
        {
            foreach (var header in responseMessage.Content.Headers)
            {
                if (!HopByHopHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Response.Headers[header.Key] = header.Value.ToArray();
                }
            }
        }

        Response.Headers.Remove("transfer-encoding");
        Response.StatusCode = (int)responseMessage.StatusCode;
        var content = await responseMessage.Content.ReadAsByteArrayAsync(cancellationToken);
        return File(content, responseMessage.Content.Headers.ContentType?.MediaType ?? "application/json");
    }

    private static string CombinePaths(string? path)
    {
        var normalized = path?.Trim('/') ?? string.Empty;
        return string.IsNullOrEmpty(normalized) ? string.Empty : normalized;
    }

    private bool RequiresSignature(Uri targetUri)
    {
        var unsignedPaths = _forwardingOptions.UnsignedPaths;
        if (unsignedPaths == null || unsignedPaths.Count == 0)
        {
            return true;
        }

        var path = targetUri.AbsolutePath;
        foreach (var pattern in unsignedPaths)
        {
            if (MatchesUnsignedPath(path, pattern))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesUnsignedPath(string actualPath, string? pattern)
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
            return normalizedActual.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase) &&
                   (normalizedActual.Length == normalizedPattern.Length || normalizedActual[normalizedPattern.Length] == '/');
        }

        return string.Equals(normalizedActual, normalizedPattern, StringComparison.OrdinalIgnoreCase);
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
}
