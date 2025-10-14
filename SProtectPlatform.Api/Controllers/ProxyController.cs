using System.Linq;
using System.Net.Http.Headers;
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

        if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var body = await reader.ReadToEndAsync(cancellationToken);
            Request.Body.Position = 0;
            requestMessage.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, Request.ContentType ?? "application/json");
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
}
