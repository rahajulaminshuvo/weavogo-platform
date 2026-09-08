using MassTransit;

namespace Weavo.BuildingBlocks.Messaging;

/// <summary>
/// MassTransit implementation of <see cref="IEventBus"/>, publishing to
/// RabbitMQ or Azure Service Bus (B.5.2, B.11).
/// </summary>
/// <remarks>
/// <para>
/// Application code depends on <see cref="IEventBus"/> rather than MassTransit's
/// <c>IPublishEndpoint</c>, so the transport stays swappable and tests can
/// assert against an in-memory collector.
/// </para>
/// <para>
/// <b>Call this only from the Outbox dispatcher.</b> Publishing inline from a
/// command handler reopens the dual-write gap B.5.3 exists to close: the
/// database commits, the broker call then fails, and the event is lost.
/// </para>
/// </remarks>
/// <param name="publishEndpoint">MassTransit's publish endpoint.</param>
public sealed class MassTransitEventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await publishEndpoint
            .Publish(integrationEvent, context => ApplyHeaders(context, integrationEvent), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishManyAsync(
        IEnumerable<IntegrationEvent> integrationEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvents);

        foreach (var integrationEvent in integrationEvents)
        {
            // Published by runtime type, not the IntegrationEvent base: MassTransit
            // routes on the message's compile-time generic argument, so publishing
            // the base type would send every event to one exchange and no typed
            // consumer would ever match.
            await publishEndpoint
                .Publish(
                    integrationEvent,
                    integrationEvent.GetType(),
                    context => ApplyHeaders(context, integrationEvent),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies correlation and tenant onto the transport envelope.
    /// </summary>
    /// <remarks>
    /// B.9 requires one traceId across HTTP, gRPC and the message bus. Setting
    /// <c>CorrelationId</c> here is what carries it across the asynchronous hop,
    /// where the ambient <c>Activity</c> does not automatically follow.
    /// </remarks>
    private static void ApplyHeaders(PublishContext context, IntegrationEvent integrationEvent)
    {
        context.MessageId = integrationEvent.EventId;

        if (Guid.TryParse(integrationEvent.CorrelationId, out var correlationId))
        {
            context.CorrelationId = correlationId;
        }

        // Header, not a claim: consumers read tenant off the envelope because a
        // message taken from a queue has no ambient HTTP context (B.6).
        context.Headers.Set("weavo-tenant-id", integrationEvent.TenantId.ToString());

        if (!string.IsNullOrWhiteSpace(integrationEvent.CorrelationId))
        {
            context.Headers.Set("weavo-trace-id", integrationEvent.CorrelationId);
        }
    }
}
