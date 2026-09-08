namespace Weavo.BuildingBlocks.Kernel;

/// <summary>
/// Non-generic view of an aggregate's pending domain events.
/// </summary>
/// <remarks>
/// <see cref="AggregateRoot{TId}"/> is generic in its key, so shared
/// infrastructure cannot filter the EF change tracker for it without knowing
/// every key type in advance. This interface gives the Outbox interceptor a
/// single type to match on, whether a context keys its aggregates by
/// <see cref="long"/> (ItemMaster's BIGINT IDENTITY) or <see cref="Guid"/>.
/// </remarks>
public interface IHasDomainEvents
{
    /// <summary>Events raised since load and not yet dispatched.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears pending events. Called by the persistence layer once they have
    /// been copied to the Outbox — never by domain code.
    /// </summary>
    void ClearDomainEvents();
}
