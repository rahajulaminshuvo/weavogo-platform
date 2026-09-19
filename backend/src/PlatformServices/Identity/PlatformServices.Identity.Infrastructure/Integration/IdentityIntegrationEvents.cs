namespace PlatformServices.Identity.Infrastructure.Integration;

using PlatformServices.Identity.Domain.Events;
using Weavo.BuildingBlocks.Infrastructure.Outbox;
using Weavo.BuildingBlocks.Messaging;

/// <summary>Published on a successful authentication. Consumed by AuditLogging.</summary>
public sealed record UserAuthenticatedIntegrationEvent : IntegrationEvent
{
    /// <summary>The authenticated account.</summary>
    public required int UserAccountId { get; init; }

    /// <summary>When authentication succeeded.</summary>
    public required DateTime AuthenticatedAtUtc { get; init; }
}

/// <summary>Published on lockout. Consumed by Notification.</summary>
public sealed record UserLockedOutIntegrationEvent : IntegrationEvent
{
    /// <summary>The locked-out account.</summary>
    public required int UserAccountId { get; init; }

    /// <summary>When the lockout expires.</summary>
    public required DateTime LockoutEndDateUtc { get; init; }
}

/// <summary>Published on password change. Consumed by AuditLogging.</summary>
public sealed record PasswordChangedIntegrationEvent : IntegrationEvent
{
    /// <summary>The account whose password changed.</summary>
    public required int UserAccountId { get; init; }
}

/// <summary>
/// Published on deactivation. Consumed by every service caching user context.
/// </summary>
public sealed record UserAccountDeactivatedIntegrationEvent : IntegrationEvent
{
    /// <summary>The deactivated account.</summary>
    public required int UserAccountId { get; init; }

    /// <summary>Login name, so consumers need not look it up.</summary>
    public required string Username { get; init; }
}

/// <summary>
/// Projects Identity's domain events onto public contracts.
/// </summary>
/// <remarks>
/// Mirrors ItemMaster's translator: domain events stay inside the bounded
/// context (B.5.2), and only the translated contract crosses the boundary, so a
/// consumer never depends on Identity's internal model. A domain event with no
/// mapping here is internal-only and the dispatcher marks it handled without
/// publishing.
/// </remarks>
public static class IdentityIntegrationEventTranslator
{
    /// <summary>
    /// Translates a deserialized domain event, or returns null when it does not
    /// cross service boundaries.
    /// </summary>
    /// <param name="domainEvent">The deserialized domain event.</param>
    /// <param name="outboxMessage">Source row, supplying identity and tenancy.</param>
    /// <returns>The public contract, or null.</returns>
    public static IntegrationEvent? Translate(object domainEvent, OutboxMessage outboxMessage)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(outboxMessage);

        return domainEvent switch
        {
            UserAuthenticatedDomainEvent e => new UserAuthenticatedIntegrationEvent
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                UserAccountId = e.UserAccountId,
                AuthenticatedAtUtc = e.AuthenticatedAtUtc,
            },

            UserLockedOutDomainEvent e => new UserLockedOutIntegrationEvent
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                UserAccountId = e.UserAccountId,
                LockoutEndDateUtc = e.LockoutEndDateUtc,
            },

            PasswordChangedDomainEvent e => new PasswordChangedIntegrationEvent
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                UserAccountId = e.UserAccountId,
            },

            UserAccountDeactivatedDomainEvent e => new UserAccountDeactivatedIntegrationEvent
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                UserAccountId = e.UserAccountId,
                Username = e.Username,
            },

            _ => null,
        };
    }
}
