using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FastRide.Shared.Models;
using Microsoft.IdentityModel.Tokens;

namespace FastRide.Api.Security;

/// <summary>Issues the JWTs the apps authenticate with.</summary>
public sealed class TokenService
{
    /// <summary>Claim carrying <see cref="User.SecurityStamp"/>, checked on every request.</summary>
    public const string SecurityStampClaim = "sstamp";

    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _lifetimeMinutes;

    public TokenService(IConfiguration config)
    {
        var secret = config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 characters.");

        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        _issuer = config["Jwt:Issuer"] ?? "FastRide";
        _audience = config["Jwt:Audience"] ?? "FastRide";
        _lifetimeMinutes = config.GetValue("Jwt:AccessTokenExpirationMinutes", 1440);
    }

    public (string Token, DateTime ExpiresAt) Issue(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_lifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(SecurityStampClaim, user.SecurityStamp.ToString())
        };

        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expiresAt, signingCredentials: _credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>Six-digit password reset code. Uses the CSPRNG, not Random.</summary>
    public static string GenerateResetCode() => RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
}
