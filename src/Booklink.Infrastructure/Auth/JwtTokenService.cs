using System.Security.Claims;
using System.Text;
using Booklink.Application.Abstractions;
using Booklink.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Booklink.Infrastructure.Auth;

public class JwtOptions
{
    public required string Key { get; set; }
    public string Issuer { get; set; } = "booklink";
    public string Audience { get; set; } = "booklink";
    public int LifetimeMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class JwtTokenService(JwtOptions options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.LifetimeMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity([
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.Name),
                new Claim("role", user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(options.SigningKey, SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }
}
