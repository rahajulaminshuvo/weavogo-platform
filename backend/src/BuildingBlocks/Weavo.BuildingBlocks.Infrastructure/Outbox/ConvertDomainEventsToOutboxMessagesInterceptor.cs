using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Weavo.BuildingBlocks.Kernel;

namespace Weavo.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Drains tracked aggregates' domain events into <see cref="OutboxMessage"/>
/// rows inside the same transaction as the business write (B.5.3).
/// </summary>
/// <remarks>
/// <para>
/// Shared by every service per B.3.4, so the reliability guarantee is
/// implemented once rather than copied into each of the 29 bounded contexts.
/// </para>
/// <para>
/// Register it on the DbContext with <c>AddInterceptors</c>. The owning context
/// must map <see cref="OutboxMessage"/>, which
/// <see cref="OutboxMessageConfiguration"/> does.
/// </para>
/// </remarks>
public sealed class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
{
    private readonly IOutboxTenantResolver? _tenantResolver;

    /// <summary>Creates the interceptor.</summary>
    /// <param name="tenantResolver">
    /// Optional strategy for stamping <see cref="OutboxMessage.TenantId"/>.
    /// When absent, tenant is left empty and the dispatcher falls back to the
    /// event payload.
    /// </param>
    public ConvertDomainEventsToOutboxMessagesInterceptor(
        IOutboxTenantResolver? tenantResolver = null)
        => _tenantResolver = tenantResolver;

    /// <inheritdoc />
    /// <remarks>
    /// EF Core dispatches the sync and async save paths independently.
    /// Overriding only the async one lets any caller of <c>SaveChanges()</c>
    /// commit while silently discarding every domain event — a failure with no
    /// exception and no log line.
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

    private void DrainDomainEvents(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        // Enumerated untyped and filtered on the CLR type. The generic
        // Entries<T>() overload matches only mapped entity types, so asking for
        // an unmapped abstract base by generic argument silently returns
        // nothing. IHasDomainEvents keeps this key-type agnostic: ItemMaster
        // uses long, other contexts use Guid.
        var aggregates = dbContext.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var messages = new List<OutboxMessage>();

        foreach (var aggregate in aggregates)
        {
            var tenantId = _tenantResolver?.ResolveTenantId(aggregate) ?? Guid.Empty;

            foreach (var domainEvent in aggregate.DomainEvents)
            {
                messages.Add(new OutboxMessage
                {
                    Id = domainEvent.EventId,
                    TenantId = tenantId,
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    Type = BuildTypeName(domainEvent.GetType()),
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                });
            }

            aggregate.ClearDomainEvents();
        }

        dbContext.Set<OutboxMessage>().AddRange(messages);
    }

    /// <summary>
    /// Builds a version-agnostic "Namespace.Type, Assembly" name.
    /// </summary>
    internal static string BuildTypeName(Type eventType)
        => $"{eventType.FullName}, {eventType.Assembly.GetName().Name}";
}

/// <summary>
/// Supplies the owning tenant for an aggregate being written to the Outbox.
/// </summary>
/// <remarks>
/// Implemented per service, since B.6's discriminator lives on each service's
/// own aggregates rather than on a shared base type.
/// </remarks>
public interface IOutboxTenantResolver
{
    /// <summary>Returns the tenant that owns the aggregate.</summary>
    /// <param name="aggregate">The aggregate being persisted.</param>
    Guid ResolveTenantId(IHasDomainEvents aggregate);
}
