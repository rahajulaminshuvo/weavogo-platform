#!/bin/bash

# Define paths based on your new 'backend' folder structure
KERNEL="backend/BuildingBlocks/Weavo.BuildingBlocks.Kernel"
APP="backend/BuildingBlocks/Weavo.BuildingBlocks.Application"
MSG="backend/BuildingBlocks/Weavo.BuildingBlocks.Messaging"
ITEM_DOMAIN="backend/MasterDataServices/ItemMaster/ItemMaster.Domain/Entities"

# 1. Create target directories
mkdir -p "$KERNEL" "$APP/Behaviors" "$MSG" "$ITEM_DOMAIN"

echo "Generating Kernel classes..."

# --- IDomainEvent.cs ---
cat > "$KERNEL/IDomainEvent.cs" <<'EOF'
namespace Weavo.BuildingBlocks.Kernel;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
EOF

# --- Entity.cs ---
cat > "$KERNEL/Entity.cs" <<'EOF'
namespace Weavo.BuildingBlocks.Kernel;

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id) => Id = id;
    protected Entity() { }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
EOF

# --- AggregateRoot.cs ---
cat > "$KERNEL/AggregateRoot.cs" <<'EOF'
namespace Weavo.BuildingBlocks.Kernel;

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public byte[]? RowVersion { get; protected set; }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
EOF

echo "Generating Application Behaviors..."

# --- LoggingBehavior.cs ---
cat > "$APP/Behaviors/LoggingBehavior.cs" <<'EOF'
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Weavo.BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var traceId = Activity.Current?.TraceId.ToString();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestName"] = requestName,
            ["TraceId"] = traceId,
        });

        logger.LogInformation("Handling {RequestName}", requestName);
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await next().ConfigureAwait(false);
            logger.LogInformation("Handled {RequestName} in {ElapsedMilliseconds:F1} ms", 
                requestName, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{RequestName} failed after {ElapsedMilliseconds:F1} ms", 
                requestName, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            throw;
        }
    }
}
EOF

echo "Generating Messaging Contracts..."

# --- IntegrationEvent.cs ---
cat > "$MSG/IntegrationEvent.cs" <<'EOF'
namespace Weavo.BuildingBlocks.Messaging;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public Guid TenantId { get; init; }
    public string? CorrelationId { get; init; }
}
EOF

# --- IEventBus.cs ---
cat > "$MSG/IEventBus.cs" <<'EOF'
namespace Weavo.BuildingBlocks.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) 
        where TEvent : IntegrationEvent;

    Task PublishManyAsync(IEnumerable<IntegrationEvent> integrationEvents, CancellationToken cancellationToken = default);
}
EOF

echo "Generating Sample Domain Entity..."

# --- ItemMasterRecord.cs (Refactored Aggregate) ---
cat > "$ITEM_DOMAIN/ItemMasterRecord.cs" <<'EOF'
using Weavo.BuildingBlocks.Kernel;

namespace ItemMaster.Domain.Entities;

public class ItemMasterRecord : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public decimal BasePrice { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core parameterless constructor
    private ItemMasterRecord() { }

    public ItemMasterRecord(Guid id, string name, decimal basePrice) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if (basePrice < 0) throw new ArgumentException("Price cannot be negative");

        Name = name;
        BasePrice = basePrice;
        IsActive = true;

        // Example: RaiseDomainEvent(new ItemCreatedDomainEvent(Id, Name));
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0) throw new ArgumentException("Price cannot be negative");
        BasePrice = newPrice;
        
        // Example: RaiseDomainEvent(new ItemPriceChangedDomainEvent(Id, newPrice));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }
}
EOF

echo "Sample codes generated successfully in the backend folder."