namespace ItemMaster.Infrastructure.Messaging;

using ItemMaster.Domain.Events;
using Weavo.BuildingBlocks.Infrastructure.Outbox;
using Weavo.BuildingBlocks.Messaging;

/// <summary>
/// Published when an item becomes orderable (B.5.2).
/// </summary>
/// <remarks>
/// Consumed by AnalyticsReporting and WeavoMM per B.5.2's event table. Carries
/// only the fields a consumer needs, not the full aggregate, so ItemMaster's
/// internal model can change without breaking subscribers.
/// </remarks>
public sealed record ItemActivated : IntegrationEvent
{
    /// <summary>Database identity of the item.</summary>
    public required long ItemId { get; init; }

    /// <summary>Business key, uppercase.</summary>
    public required string ItemCode { get; init; }

    /// <summary>Human-readable item name.</summary>
    public required string ItemName { get; init; }

    /// <summary>Stocking unit of measure.</summary>
    public required int BaseUomId { get; init; }
}

/// <summary>Published when an item is registered in the catalogue.</summary>
public sealed record ItemRegistered : IntegrationEvent
{
    /// <summary>Database identity of the item.</summary>
    public required long ItemId { get; init; }

    /// <summary>Business key, uppercase.</summary>
    public required string ItemCode { get; init; }

    /// <summary>Human-readable item name.</summary>
    public required string ItemName { get; init; }

    /// <summary>Direct parent in the classification hierarchy.</summary>
    public required int ItemFamilyId { get; init; }

    /// <summary>Stocking unit of measure.</summary>
    public required int BaseUomId { get; init; }
}

/// <summary>
/// Published when an item's lifecycle state changes.
/// </summary>
public sealed record ItemStatusChanged : IntegrationEvent
{
    /// <summary>Database identity of the item.</summary>
    public required long ItemId { get; init; }

    /// <summary>Business key, uppercase.</summary>
    public required string ItemCode { get; init; }

    /// <summary>Status before the change.</summary>
    public required string OldStatus { get; init; }

    /// <summary>Status after the change.</summary>
    public required string NewStatus { get; init; }
}

/// <summary>
/// Maps internal domain events onto the public integration contracts.
/// </summary>
/// <remarks>
/// <para>
/// B.5.2 draws a hard line: a domain event stays inside its bounded context; only
/// a translated integration event crosses the boundary. Publishing the domain
/// event directly would make every consumer depend on ItemMaster's internal
/// model, so an internal refactor would break them.
/// </para>
/// <para>
/// A domain event with no mapping here is internal-only, and the dispatcher marks
/// it handled without publishing.
/// </para>
/// </remarks>
public static class IntegrationEventTranslator
{
    /// <summary>
    /// Translates a deserialized domain event, or returns null when the event
    /// is internal to this service.
    /// </summary>
    /// <param name="domainEvent">The deserialized domain event.</param>
    /// <param name="outboxMessage">Source row, supplying tenant and correlation.</param>
    /// <returns>The public contract, or null if the event does not cross boundaries.</returns>
    public static IntegrationEvent? Translate(object domainEvent, OutboxMessage outboxMessage)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(outboxMessage);

        return domainEvent switch
        {
            ItemCreatedDomainEvent e => new ItemRegistered
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                ItemId = e.ItemId,
                ItemCode = e.ItemCode,
                ItemName = e.ItemName,
                ItemFamilyId = e.ItemFamilyId,
                BaseUomId = e.BaseUOMId,
            },

            // "Active" is the transition B.5.2's ItemActivated describes: the
            // point at which WeavoMM may order the item. Other transitions
            // (Draft -> PendingApproval) interest nobody outside this service.
            ItemStatusChangedDomainEvent e when e.NewStatus == "Active" => new ItemActivated
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                ItemId = e.ItemId,
                ItemCode = e.ItemCode,
                ItemName = e.ItemCode,
                BaseUomId = 0,
            },

            ItemStatusChangedDomainEvent e => new ItemStatusChanged
            {
                EventId = outboxMessage.Id,
                OccurredOnUtc = outboxMessage.OccurredOnUtc,
                TenantId = outboxMessage.TenantId,
                ItemId = e.ItemId,
                ItemCode = e.ItemCode,
                OldStatus = e.OldStatus,
                NewStatus = e.NewStatus,
            },

            // ItemPriceUpdatedDomainEvent has no cross-service consumer in B.5.2.
            _ => null,
        };
    }
}
