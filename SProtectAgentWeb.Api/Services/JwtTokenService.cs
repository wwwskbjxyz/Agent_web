using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Services;

public class JwtTokenService
{
    private readonly JwtConfig _jwtConfig;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(AppConfig appConfig, ILogger<JwtTokenService> logger)
    {
        _jwtConfig = appConfig.Jwt;
        _logger = logger;
    }

    public JwtTokenResult CreateAccessToken(UserSession session)
    {
        if (string.IsNullOrWhiteSpace(_jwtConfig.Secret))
        {
            throw new InvalidOperationException("JWT密钥未配置");
        }

        if (_jwtConfig.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT密钥长度至少需要32位以确保安全");
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(_jwtConfig.AccessTokenExpirationMinutes, 5, 1440));
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, session.Username),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(ClaimTypes.Name, session.Username)
        };

        if (!string.IsNullOrWhiteSpace(session.IpAddress))
        {
            claims.Add(new Claim("ip", session.IpAddress));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_jwtConfig.Issuer) ? null : _jwtConfig.Issuer,
            audience: string.IsNullOrWhiteSpace(_jwtConfig.Audience) ? null : _jwtConfig.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.WriteToken(tokenDescriptor);

        _logger.LogInformation("Issued access token for {Username} expiring at {Expires}", session.Username, expires);

        return new JwtTokenResult(token, tokenId, expires);
    }
}

public record JwtTokenResult(string Token, string TokenId, DateTimeOffset ExpiresAtUtc);
