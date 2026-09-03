namespace ItemMaster.Domain.Events;

using Weavo.BuildingBlocks.Kernel;

/// <summary>Raised once a new item has been persisted and assigned an identity.</summary>
/// <param name="ItemId">Database identity of the item.</param>
/// <param name="ItemCode">Business key, uppercase.</param>
/// <param name="ItemName">Human-readable item name.</param>
/// <param name="ItemFamilyId">Direct parent in the classification hierarchy.</param>
/// <param name="BaseUOMId">Stocking unit of measure.</param>
/// <param name="CreatedBy">User who created the item.</param>
/// <remarks>
/// Raised from <c>ItemMasterRecord.RaiseCreatedEvent()</c> after the INSERT, not
/// from the constructor: <c>ItemId</c> is IDENTITY and is still 0 beforehand.
/// </remarks>
public sealed record ItemCreatedDomainEvent(
    long ItemId,
    string ItemCode,
    string ItemName,
    int ItemFamilyId,
    int BaseUOMId,
    int CreatedBy
) : DomainEvent;

/// <summary>Raised when an item's price changes.</summary>
/// <param name="ItemId">Database identity of the item.</param>
/// <param name="ItemCode">Business key.</param>
/// <param name="OldPrice">Price before the change.</param>
/// <param name="NewPrice">Price after the change.</param>
/// <param name="ModifiedBy">User who made the change.</param>
public sealed record ItemPriceUpdatedDomainEvent(
    long ItemId,
    string ItemCode,
    decimal OldPrice,
    decimal NewPrice,
    int ModifiedBy
) : DomainEvent;

/// <summary>Raised when an item moves through its lifecycle states.</summary>
/// <param name="ItemId">Database identity of the item.</param>
/// <param name="ItemCode">Business key.</param>
/// <param name="OldStatus">Status before the change.</param>
/// <param name="NewStatus">Status after the change.</param>
/// <param name="ModifiedBy">User who made the change.</param>
public sealed record ItemStatusChangedDomainEvent(
    long ItemId,
    string ItemCode,
    string OldStatus,
    string NewStatus,
    int ModifiedBy
) : DomainEvent;

/// <summary>
/// One immutable record of something that happened to an item, for the
/// append-only audit trail.
/// </summary>
public sealed record ItemAuditEntry
{
    /// <summary>Identity of this audit entry.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Item the entry concerns.</summary>
    public long ItemId { get; init; }

    /// <summary>The item's business key at the time of the change.</summary>
    public string ItemCode { get; init; } = string.Empty;

    /// <summary>Domain event type name, for example <c>ItemCreatedDomainEvent</c>.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>JSON-serialised event payload.</summary>
    public string EventData { get; init; } = string.Empty;

    /// <summary>User who triggered the change.</summary>
    public int TriggeredBy { get; init; }

    /// <summary>UTC instant of the change.</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
