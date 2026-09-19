// TODO(Phase 2): replace with Entra ID federation per ADR-007. The symmetric
// key and every JwtSecurityToken call below disappear when credential
// verification moves to Entra; IJwtTokenGenerator is the seam that keeps that
// change inside this file.
namespace PlatformServices.Identity.Infrastructure.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlatformServices.Identity.Application.Common.Security;
using PlatformServices.Identity.Application.Contracts;
using PlatformServices.Identity.Application.DTOs;

/// <summary>JWT issuance settings, bound from <c>JwtSettings</c>.</summary>
public sealed class JwtSettings
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// HS256 signing key. Never committed: supplied via user-secrets locally or
    /// the platform vault in a container.
    /// </summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Token issuer, validated by every downstream service.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Token audience, validated by every downstream service.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Access-token lifetime.</summary>
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>Refresh-token lifetime.</summary>
    public int RefreshTokenExpiryDays { get; init; } = 7;
}

/// <summary>
/// Mints HS256 tokens carrying the tenancy claims every service filters on (B.6, B.7.1).
/// </summary>
/// <param name="options">Issuer, audience, key and lifetimes.</param>
public sealed class JwtTokenGenerator(IOptions<JwtSettings> options) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = options.Value;

    /// <inheritdoc />
    public TokenResponse GenerateToken(UserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        var handler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, userContext.UserAccountId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),

            // ClaimTypes.NameIdentifier as well as "sub": Microsoft's JWT handler
            // rewrites "sub" to NameIdentifier during validation, so downstream
            // services reading either name find the value.
            new(ClaimTypes.NameIdentifier, userContext.UserAccountId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),

            new(CustomClaimTypes.UserAccountId, userContext.UserAccountId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            new(CustomClaimTypes.Email, userContext.Email),
            new(CustomClaimTypes.TenantId, userContext.TenantId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            new(CustomClaimTypes.CompanyId, userContext.CompanyId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
            new(CustomClaimTypes.BusinessUnitId, userContext.BusinessUnitId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
        };

        foreach (var role in userContext.Roles)
        {
            // Both names: ClaimTypes.Role drives [Authorize(Roles = ...)], while
            // the short "role" claim is what policy checks in other services read.
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim(CustomClaimTypes.Role, role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = handler.CreateToken(descriptor);

        return new TokenResponse(
            AccessToken: handler.WriteToken(token),
            RefreshToken: GenerateRefreshToken(),
            ExpiresInSeconds: _settings.ExpiryMinutes * 60);
    }

    /// <summary>
    /// Produces an opaque 64-byte refresh token.
    /// </summary>
    /// <remarks>
    /// Cryptographically random rather than a second JWT: a refresh token
    /// carries no claims and should be unguessable and revocable, which a
    /// self-describing signed token is not.
    /// </remarks>
    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>BCrypt password hashing.</summary>
/// <remarks>
/// Work factor 12: roughly 250ms per verification on current hardware, which is
/// slow enough to make offline brute force expensive and fast enough for an
/// interactive login.
/// </remarks>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return BCrypt.Net.BCrypt.HashPassword(plaintext, WorkFactor);
    }

    /// <inheritdoc />
    public bool Verify(string plaintext, string hash)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plaintext, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed stored hash must read as "wrong password", not crash
            // the request and reveal that the record is corrupt.
            return false;
        }
    }
}
