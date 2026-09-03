namespace Weavo.BuildingBlocks.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) 
        where TEvent : IntegrationEvent;

    Task PublishManyAsync(IEnumerable<IntegrationEvent> integrationEvents, CancellationToken cancellationToken = default);
}
