using System.Linq;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Services;

namespace SProtectPlatform.Api.Utilities;

public sealed class DatabaseCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly ICorsOriginService _originService;
    private readonly ILogger<DatabaseCorsPolicyProvider> _logger;

    public DatabaseCorsPolicyProvider(ICorsOriginService originService, ILogger<DatabaseCorsPolicyProvider> logger)
    {
        _originService = originService;
        _logger = logger;
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origins = await _originService.GetAllowedOriginsAsync(context.RequestAborted);
        var policy = new CorsPolicyBuilder()
            .WithOrigins(origins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .Build();

        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin) && !origins.Contains(origin))
        {
            _logger.LogWarning("Rejected CORS request from origin {Origin}", origin);
        }

        return policy;
    }
}
