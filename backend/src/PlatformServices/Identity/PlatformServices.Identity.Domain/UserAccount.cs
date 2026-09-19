namespace PlatformServices.Identity.Domain;

using PlatformServices.Identity.Domain.Events;
using Weavo.BuildingBlocks.Kernel;

/// <summary>
/// A login record (Appendix A.12.2) — deliberately thin.
/// </summary>
/// <remarks>
/// <para>
/// A.12.2 describes this as "a thin login record referencing Employee, never
/// duplicating a person's name a second time." Credential state lives in
/// <see cref="UserCredential"/>: it changes on every login attempt, and a
/// federated Phase 2 removes it entirely without touching this aggregate.
/// </para>
/// <para>
/// <c>UserAccountId</c> is <c>INT IDENTITY</c>, so <see cref="Entity{TId}.Id"/>
/// is 0 until the INSERT commits. Creation events are therefore raised by the
/// persistence layer after the insert, never from the factory — the same
/// timing hazard ItemMaster hit.
/// </para>
/// </remarks>
public sealed class UserAccount : AggregateRoot<int>
{
    /// <summary>Login name, unique across the platform.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>Login and notification address (A.12.2).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// The employment this login belongs to. Null for service accounts, which
    /// resolve tenancy from an explicit assignment instead (Phase 2).
    /// </summary>
    public int? LinkedPersonId { get; private set; }

    /// <summary>
    /// Whether the login is usable. Set false on separation, in the same
    /// transaction as the IT clearance line (A.12.2 business rules) — access
    /// revocation is part of clearance, not a separate manual step.
    /// </summary>
    public bool IsActive { get; private set; }

    private UserAccount()
    {
    }

    /// <summary>Registers a new login.</summary>
    /// <param name="username">Login name; must be unique.</param>
    /// <param name="email">Login and notification address.</param>
    /// <param name="linkedPersonId">Employment this login belongs to, if any.</param>
    /// <returns>The new account, pending insert.</returns>
    /// <exception cref="ArgumentException">Thrown when username or email is blank.</exception>
    public static UserAccount Create(string username, string email, int? linkedPersonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new UserAccount
        {
            Username = username.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            LinkedPersonId = linkedPersonId,
            IsActive = true,
        };
    }

    /// <summary>Revokes access without deleting the record.</summary>
    /// <remarks>Idempotent: deactivating an inactive account raises no event.</remarks>
    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RaiseDomainEvent(new UserAccountDeactivatedDomainEvent(Id, Username));
    }

    /// <summary>Restores access. Idempotent.</summary>
    public void Activate() => IsActive = true;
}
