namespace Weavo.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// A domain event persisted in the same local transaction as the aggregate
/// change that produced it (B.5.3).
/// </summary>
/// <remarks>
/// <para>
/// The Outbox row and the business data commit or roll back together, so there
/// is no window in which the aggregate changed but the event was lost, nor one
/// in which the event fired but the change never committed.
/// </para>
/// <para>
/// The table lives in the owning service's own database, whichever engine
/// B.4.8 assigns it — the single local transaction is the entire point, so the
/// Outbox can never be centralised into a shared store.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>
    /// Identity of the message, taken from the domain event's own EventId.
    /// </summary>
    /// <remarks>
    /// Consumers deduplicate on this value (B.5.3). A freshly generated Guid
    /// here would defeat that, since a redelivered message would look new.
    /// </remarks>
    public Guid Id { get; set; }

    /// <summary>
    /// Owning tenant, carried on the row so the dispatcher can stamp it onto
    /// the integration event without an ambient HTTP context (B.6).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// CLR type in "Namespace.Type, Assembly" form.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Type.AssemblyQualifiedName"/>: that embeds
    /// Version=1.0.0.0, so rows written by one build stop resolving after a
    /// version bump and the dispatcher parks them all as unresolvable.
    /// </remarks>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON-serialised event payload.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC instant the originating state transition occurred.</summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// UTC instant the broker confirmed receipt; null while unpublished.
    /// </summary>
    /// <remarks>
    /// Stamped only after MassTransit confirms, never before. A crash between
    /// publish and stamp replays the message — which is precisely why delivery
    /// is at-least-once and consumers must be idempotent.
    /// </remarks>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// Failure detail from the last dispatch attempt; null on success.
    /// Retained so a poisoned message can be diagnosed rather than silently retried.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Dispatch attempts so far, used to stop retrying a permanently broken row.
    /// </summary>
    public int AttemptCount { get; set; }
}
