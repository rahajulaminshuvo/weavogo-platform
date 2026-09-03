namespace ItemMaster.Infrastructure.Persistence.Outbox;

/// <summary>
/// A domain event persisted alongside the business data that produced it,
/// awaiting dispatch.
/// </summary>
/// <remarks>
/// This row is written in the same transaction as the aggregate change, which
/// is what makes the Outbox pattern work: either the item change is committed
/// and its event queued, or neither happens.
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>
    /// Identity of the message. Set from the domain event's own
    /// <c>EventId</c> so consumers can deduplicate under at-least-once delivery.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// CLR type name in "Namespace.Type, Assembly" form, used to rehydrate the
    /// payload. Deliberately not assembly-qualified; see the interceptor.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON-serialised event payload.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC instant the originating event occurred.</summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>UTC instant the message was dispatched; null while pending.</summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>
    /// Failure detail from the last dispatch attempt, null on success. Retained
    /// so a poisoned message can be diagnosed rather than silently retried.
    /// </summary>
    public string? Error { get; set; }
}
