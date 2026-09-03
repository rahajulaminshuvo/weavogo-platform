namespace ItemMaster.Infrastructure.Persistence.Interceptors;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Weavo.BuildingBlocks.Kernel;
using ItemMaster.Infrastructure.Persistence.Outbox;

/// <summary>
/// Drains each tracked aggregate's domain events into <see cref="OutboxMessage"/>
/// rows as part of the same transaction as the business write.
/// </summary>
/// <remarks>
/// Because the rows join the change set before the transaction commits, a state
/// change and the events announcing it either both persist or neither does.
/// That closes the dual-write hole: no event can be lost because the broker was
/// unreachable at the moment of the commit.
/// </remarks>
public sealed class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
{
    /// <summary>Drains domain events on the synchronous save path.</summary>
    /// <param name="eventData">Context and metadata for the save.</param>
    /// <param name="result">The interception result to pass along.</param>
    /// <returns>The base interception result.</returns>
    /// <remarks>
    /// EF Core dispatches <c>SavingChanges</c> and <c>SavingChangesAsync</c>
    /// independently. Overriding only the async one lets any caller of
    /// <c>SaveChanges()</c> commit successfully while silently dropping every
    /// domain event -- a failure with no exception and no log line.
    /// </remarks>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        DrainDomainEvents(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        DrainDomainEvents(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Copies pending domain events onto the change set as Outbox rows and
    /// clears them from the aggregates.
    /// </summary>
    /// <param name="dbContext">The context being saved; null outside a context.</param>
    private static void DrainDomainEvents(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        // Entries() is enumerated untyped and filtered on the CLR type with
        // OfType. The generic Entries<T>() overload matches only entity types
        // EF Core has mapped, so an unmapped abstract base can silently return
        // nothing; filtering here stays correct regardless.
        var aggregates = dbContext.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<AggregateRoot<long>>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var outboxMessages = new List<OutboxMessage>();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                outboxMessages.Add(new OutboxMessage
                {
                    // The event's own identity, not a fresh Guid: the dispatcher
                    // and downstream consumers deduplicate on this value, and a
                    // new id per drain would defeat that.
                    Id = domainEvent.EventId,
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    Type = BuildTypeName(domainEvent.GetType()),
                    Content = JsonSerializer.Serialize(
                        domainEvent, domainEvent.GetType()),
                });
            }

            aggregate.ClearDomainEvents();
        }

        dbContext.Set<OutboxMessage>().AddRange(outboxMessages);
    }

    /// <summary>
    /// Produces a type name that <see cref="Type.GetType(string)"/> resolves and
    /// that stays valid across assembly version bumps.
    /// </summary>
    /// <param name="eventType">The concrete domain event type.</param>
    /// <returns>A "Namespace.Type, Assembly" type name.</returns>
    /// <remarks>
    /// <see cref="Type.AssemblyQualifiedName"/> embeds <c>Version=1.0.0.0</c>.
    /// Rows written by one build then fail to resolve after the version changes,
    /// and the dispatcher parks them all as "Type resolution failed" -- silent
    /// event loss triggered by nothing more than a release. The two-part form
    /// resolves identically and is stable, and is shorter, which keeps the
    /// <c>varchar(255)</c> column comfortable for deeper namespaces.
    /// </remarks>
    private static string BuildTypeName(Type eventType)
        => $"{eventType.FullName}, {eventType.Assembly.GetName().Name}";
}
