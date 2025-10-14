using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Services;

namespace SProtectAgentWeb.Api.Sessions;

public class SessionManager
{
    private const string SessionKey = "user_info";
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly TokenSessionStore _tokenSessionStore;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(IHttpContextAccessor contextAccessor, TokenSessionStore tokenSessionStore, ILogger<SessionManager> logger)
    {
        _contextAccessor = contextAccessor;
        _tokenSessionStore = tokenSessionStore;
        _logger = logger;
    }

    public void SetUserSession(UserSession session, string tokenId, DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        _tokenSessionStore.Set(tokenId, session, expiresAtUtc);
        _logger.LogDebug("Session stored for {Username} with token {TokenId} until {Expires}", session.Username, tokenId, expiresAtUtc);
    }

    public UserSession? GetUserSession()
    {
        var httpContext = _contextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var tokenId = GetCurrentTokenId(httpContext.User);
        if (!string.IsNullOrEmpty(tokenId))
        {
            var session = _tokenSessionStore.Get(tokenId);
            if (session is not null)
            {
                return session;
            }
        }

        var payload = httpContext.Session.GetString(SessionKey);
        return string.IsNullOrEmpty(payload) ? null : JsonSerializer.Deserialize<UserSession>(payload);
    }

    public DateTimeOffset? GetCurrentTokenExpiration()
    {
        var httpContext = _contextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var tokenId = GetCurrentTokenId(httpContext.User);
        return string.IsNullOrEmpty(tokenId) ? null : _tokenSessionStore.GetExpiration(tokenId);
    }

    public void ClearUserSession()
    {
        var httpContext = _contextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var tokenId = GetCurrentTokenId(httpContext.User);
        if (!string.IsNullOrEmpty(tokenId))
        {
            _tokenSessionStore.Remove(tokenId);
        }

        httpContext.Session.Remove(SessionKey);
    }

    private static string? GetCurrentTokenId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(JwtRegisteredClaimNames.Jti) ?? user.FindFirstValue(ClaimTypes.Sid);
    }
}
