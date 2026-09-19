namespace PlatformServices.Identity.Domain.Events;

using Weavo.BuildingBlocks.Kernel;

/// <summary>Raised on a successful authentication.</summary>
/// <param name="UserAccountId">The authenticated account.</param>
/// <param name="AuthenticatedAtUtc">When it happened.</param>
public sealed record UserAuthenticatedDomainEvent(
    int UserAccountId,
    DateTime AuthenticatedAtUtc) : DomainEvent;

/// <summary>Raised when consecutive failures trip the lockout threshold.</summary>
/// <param name="UserAccountId">The locked-out account.</param>
/// <param name="LockoutEndDateUtc">When the lockout expires.</param>
public sealed record UserLockedOutDomainEvent(
    int UserAccountId,
    DateTime LockoutEndDateUtc) : DomainEvent;

/// <summary>Raised when a password is changed.</summary>
/// <param name="UserAccountId">The account whose password changed.</param>
public sealed record PasswordChangedDomainEvent(int UserAccountId) : DomainEvent;

/// <summary>Raised when access is revoked.</summary>
/// <param name="UserAccountId">The deactivated account.</param>
/// <param name="Username">Login name, so consumers need not look it up.</param>
public sealed record UserAccountDeactivatedDomainEvent(
    int UserAccountId,
    string Username) : DomainEvent;
