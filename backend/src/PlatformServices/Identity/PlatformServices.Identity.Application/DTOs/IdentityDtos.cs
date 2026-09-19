namespace PlatformServices.Identity.Application.DTOs;

/// <summary>Credentials plus the scope the caller wants a token for.</summary>
/// <param name="Username">Login name.</param>
/// <param name="Password">Plaintext password, verified against a BCrypt hash.</param>
/// <param name="BusinessUnitId">
/// Requested scope. A.12.3 scopes each role grant to a business unit, so a user
/// with grants in several units must say which one this token is for. When
/// omitted, the user's single grant is used; ambiguity is rejected rather than
/// guessed.
/// </param>
public sealed record TokenGenerationRequest(
    string Username,
    string Password,
    int? BusinessUnitId = null);

/// <summary>A refresh-token exchange.</summary>
/// <param name="RefreshToken">The opaque refresh token previously issued.</param>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>An issued token pair.</summary>
/// <param name="AccessToken">Signed JWT.</param>
/// <param name="RefreshToken">Opaque, cryptographically random.</param>
/// <param name="ExpiresInSeconds">Access-token lifetime.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    string TokenType = "Bearer");

/// <summary>
/// The resolved identity a token is minted from.
/// </summary>
/// <param name="UserAccountId">Surrogate key of the account.</param>
/// <param name="Username">Login name.</param>
/// <param name="Email">Login and notification address.</param>
/// <param name="TenantId">Outermost tenancy boundary.</param>
/// <param name="CompanyId">Company within the tenant.</param>
/// <param name="BusinessUnitId">Business unit this token is scoped to.</param>
/// <param name="Roles">Role codes held in that unit, and only that unit.</param>
public sealed record UserContext(
    int UserAccountId,
    string Username,
    string Email,
    int TenantId,
    int CompanyId,
    int BusinessUnitId,
    IReadOnlyList<string> Roles);
