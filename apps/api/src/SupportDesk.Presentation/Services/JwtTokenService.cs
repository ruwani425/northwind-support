using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Services;

/// <summary>
/// Issues cryptographically signed JSON Web Tokens (JWT) for authenticated agents.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(Agent agent)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured. Set 'Jwt:SigningKey' or 'Jwt__SigningKey'.");
        }

        var now = clock.UtcNow;
        var expiresAtUtc = now.AddHours(_options.LifetimeHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new(ClaimTypes.Email, agent.Email),
            new(ClaimTypes.Name, agent.FullName),
            new(ClaimTypes.Role, agent.Role),
            new("role", agent.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            NotBefore = now,
            IssuedAt = now,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var tokenString = handler.WriteToken(token);

        return (tokenString, expiresAtUtc);
    }
}
