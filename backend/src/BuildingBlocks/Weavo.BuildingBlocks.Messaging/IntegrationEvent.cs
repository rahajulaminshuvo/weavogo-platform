namespace Weavo.BuildingBlocks.Messaging;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public Guid TenantId { get; init; }
    public string? CorrelationId { get; init; }
}
