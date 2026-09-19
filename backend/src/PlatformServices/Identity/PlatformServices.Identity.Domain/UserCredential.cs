namespace PlatformServices.Identity.Domain;

using PlatformServices.Identity.Domain.Events;
using Weavo.BuildingBlocks.Kernel;

/// <summary>How a credential authenticates.</summary>
public static class CredentialTypes
{
    /// <summary>Locally verified password hash (Phase 1).</summary>
    public const string Password = "Password";

    /// <summary>Federated to an external OIDC provider (Phase 2, ADR-007).</summary>
    public const string ExternalOidc = "ExternalOIDC";
}

/// <summary>
/// Authentication state for a <see cref="UserAccount"/>, kept as its own
/// aggregate.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="UserAccount"/> for three reasons: it is written on
/// every login attempt and should not dirty the account row; Phase 2's Entra ID
/// federation removes <see cref="PasswordHash"/> without touching the account;
/// and an account may legitimately exist before a credential is set.
/// </para>
/// <para>
/// TODO(Phase 2): replace password verification with Entra ID federation per
/// ADR-007. <see cref="CredentialType"/> already carries the discriminator.
/// </para>
/// </remarks>
public sealed class UserCredential : AggregateRoot<int>
{
    /// <summary>Maximum consecutive failures before lockout.</summary>
    public const int DefaultMaxFailedAttempts = 5;

    /// <summary>How long a lockout lasts.</summary>
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>The account this credential authenticates. One per account in Phase 1.</summary>
    public int UserAccountId { get; private set; }

    /// <summary>One of <see cref="CredentialTypes"/>.</summary>
    public string CredentialType { get; private set; } = CredentialTypes.Password;

    /// <summary>BCrypt hash. Null when federated.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Consecutive failures since the last success.</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>When the current lockout expires; null when not locked out.</summary>
    public DateTime? LockoutEndDate { get; private set; }

    /// <summary>When the password was last changed.</summary>
    public DateTime? LastPasswordChangeDate { get; private set; }

    /// <summary>When the last successful login occurred.</summary>
    public DateTime? LastLoginDate { get; private set; }

    private UserCredential()
    {
    }

    /// <summary>Creates a password credential.</summary>
    /// <param name="userAccountId">Account this authenticates.</param>
    /// <param name="passwordHash">A BCrypt hash, never a plaintext password.</param>
    /// <returns>The new credential, pending insert.</returns>
    public static UserCredential CreatePassword(int userAccountId, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new UserCredential
        {
            UserAccountId = userAccountId,
            CredentialType = CredentialTypes.Password,
            PasswordHash = passwordHash,
            LastPasswordChangeDate = DateTime.UtcNow,
        };
    }

    /// <summary>Whether the credential is currently locked out.</summary>
    public bool IsLockedOut() =>
        LockoutEndDate.HasValue && LockoutEndDate.Value > DateTime.UtcNow;

    /// <summary>Records a successful authentication and clears failure state.</summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginDate = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndDate = null;

        RaiseDomainEvent(new UserAuthenticatedDomainEvent(UserAccountId, LastLoginDate.Value));
    }

    /// <summary>
    /// Records a failed attempt, locking the credential once the threshold is hit.
    /// </summary>
    /// <param name="maxAttempts">Failures tolerated before lockout.</param>
    /// <param name="lockoutDuration">How long the lockout lasts.</param>
    public void RecordFailedLogin(
        int maxAttempts = DefaultMaxFailedAttempts,
        TimeSpan? lockoutDuration = null)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts < maxAttempts)
        {
            return;
        }

        LockoutEndDate = DateTime.UtcNow.Add(lockoutDuration ?? DefaultLockoutDuration);
        RaiseDomainEvent(new UserLockedOutDomainEvent(UserAccountId, LockoutEndDate.Value));
    }

    /// <summary>Replaces the password hash and clears any lockout.</summary>
    /// <param name="newHash">A BCrypt hash, never a plaintext password.</param>
    public void ChangePassword(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);

        PasswordHash = newHash;
        LastPasswordChangeDate = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndDate = null;

        RaiseDomainEvent(new PasswordChangedDomainEvent(UserAccountId));
    }
}
