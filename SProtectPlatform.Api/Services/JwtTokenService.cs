using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(string subject, string role, IReadOnlyDictionary<string, string>? additionalClaims = null);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKey;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
        }

        _signingKey = Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    public (string Token, DateTime ExpiresAt) CreateToken(string subject, string role, IReadOnlyDictionary<string, string>? additionalClaims = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(Math.Max(_options.AccessTokenMinutes, 30));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Role, role)
        };

        if (additionalClaims != null)
        {
            foreach (var pair in additionalClaims)
            {
                claims.Add(new Claim(pair.Key, pair.Value));
            }
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expires);
    }
}
