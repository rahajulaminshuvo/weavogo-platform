namespace PlatformServices.Identity.Application.Contracts;

using PlatformServices.Identity.Application.DTOs;
using PlatformServices.Identity.Domain;

/// <summary>Persistence gateway for <see cref="UserAccount"/>.</summary>
/// <remarks>
/// Declared here and implemented in Infrastructure, so handlers never take a
/// DbContext - the same inversion ItemMaster uses.
/// </remarks>
public interface IUserAccountRepository
{
    /// <summary>Loads an active-or-inactive account by login name.</summary>
    /// <param name="username">Login name, matched case-insensitively.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>Loads an account by surrogate key.</summary>
    /// <param name="userAccountId">The key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<UserAccount?> FindByIdAsync(int userAccountId, CancellationToken cancellationToken);

    /// <summary>Stages a new account for insertion.</summary>
    /// <param name="account">The account to add.</param>
    void Add(UserAccount account);

    /// <summary>Commits staged changes.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Persistence gateway for <see cref="UserCredential"/>.</summary>
/// <remarks>
/// Separate from <see cref="IUserAccountRepository"/> because the two are
/// separate aggregates with very different write frequencies.
/// </remarks>
public interface IUserCredentialRepository
{
    /// <summary>Loads the credential for an account, if one has been set.</summary>
    /// <param name="userAccountId">The owning account.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<UserCredential?> FindByUserAccountIdAsync(
        int userAccountId, CancellationToken cancellationToken);

    /// <summary>Stages a new credential for insertion.</summary>
    /// <param name="credential">The credential to add.</param>
    void Add(UserCredential credential);

    /// <summary>Commits staged changes.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the tenancy a token should be scoped to.
/// </summary>
/// <remarks>
/// A.12.3 scopes each role grant to a BusinessUnitId, so tenancy is a property
/// of the grant rather than of the user. This walks
/// UserAccount to UserRole to BusinessUnit to Company to Tenant and returns the
/// roles held in that one unit.
/// </remarks>
public interface IUserContextResolver
{
    /// <summary>Resolves the scope, or null when the user has no usable grant.</summary>
    /// <param name="userAccount">The authenticated account.</param>
    /// <param name="requestedBusinessUnitId">
    /// The unit the caller asked for. When null and the user holds grants in
    /// exactly one unit, that unit is used; when null and several exist, the
    /// request is ambiguous and null is returned.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<UserContext?> ResolveAsync(
        UserAccount userAccount,
        int? requestedBusinessUnitId,
        CancellationToken cancellationToken);
}

/// <summary>Mints signed tokens.</summary>
/// <remarks>
/// The seam ADR-007's Phase 2 swings on: replacing the HS256 implementation
/// with Entra ID federation must not touch Application or Api.
/// </remarks>
public interface IJwtTokenGenerator
{
    /// <summary>Mints an access/refresh pair for a resolved identity.</summary>
    /// <param name="userContext">The resolved identity and scope.</param>
    TokenResponse GenerateToken(UserContext userContext);
}

/// <summary>Hashes and verifies passwords.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password.</summary>
    /// <param name="plaintext">The password.</param>
    string Hash(string plaintext);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    /// <param name="plaintext">The candidate password.</param>
    /// <param name="hash">The stored hash.</param>
    bool Verify(string plaintext, string hash);
}
