namespace ItemMaster.Infrastructure.BackgroundJobs;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ItemMaster.Infrastructure.Persistence;
using Weavo.BuildingBlocks.Infrastructure.Outbox;
using ItemMaster.Infrastructure.Messaging;
using Weavo.BuildingBlocks.Messaging;

/// <summary>
/// Polls the Outbox and hands unpublished rows to MassTransit (B.5.3).
/// </summary>
/// <remarks>
/// <para>
/// A row is marked processed only after the broker confirms receipt. The
/// processor can still fail between publish and stamp, so delivery is
/// <b>at-least-once</b>: consumers deduplicate via
/// <c>IIdempotencyStore</c> rather than assuming the broker delivers once.
/// </para>
/// <para>
/// A row that fails repeatedly is abandoned after
/// <see cref="MaxAttempts"/> tries with its error retained, so one poisoned
/// message cannot block the queue behind it. Query
/// <c>WHERE Error IS NOT NULL</c> to find them.
/// </para>
/// </remarks>
/// <param name="serviceScopeFactory">Creates a scope per polling pass.</param>
/// <param name="logger">Logger for dispatch failures.</param>
public sealed partial class ProcessOutboxMessagesJob(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProcessOutboxMessagesJob> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopFailed(logger, ex);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ItemMasterDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null && m.AttemptCount < MaxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(stoppingToken)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            message.AttemptCount++;

            try
            {
                var eventType = Type.GetType(message.Type);

                if (eventType is null)
                {
                    LogTypeResolutionFailed(logger, message.Type, message.Id);
                    message.Error = "Type resolution failed";
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Content, eventType);

                if (payload is null)
                {
                    message.Error = "Payload deserialized to null";
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    continue;
                }

                // Domain events stay inside this bounded context (B.5.2); only a
                // translated IntegrationEvent crosses the boundary, so a consumer
                // never takes a dependency on ItemMaster's internal model.
                var integrationEvent = IntegrationEventTranslator.Translate(payload, message);

                if (integrationEvent is null)
                {
                    // Internal-only event: nothing subscribes across services.
                    // Mark handled so it is not retried forever.
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    continue;
                }

                await eventBus
                    .PublishAsync(integrationEvent, stoppingToken)
                    .ConfigureAwait(false);

                // Stamped only after the broker confirms. A crash before this
                // line replays the message - which is exactly why consumers
                // must be idempotent.
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMessageFailed(logger, message.Id, message.AttemptCount, ex);
                message.Error = ex.ToString();

                if (message.AttemptCount >= MaxAttempts)
                {
                    LogMessageAbandoned(logger, message.Id, MaxAttempts);
                    message.ProcessedOnUtc = DateTime.UtcNow;
                }
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Could not resolve type {Type} for outbox message {Id}")]
    private static partial void LogTypeResolutionFailed(ILogger logger, string type, Guid id);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Failed to publish outbox message {Id} (attempt {Attempt})")]
    private static partial void LogMessageFailed(
        ILogger logger, Guid id, int attempt, Exception exception);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Error occurred while executing Outbox processing loop")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Critical,
        Message = "Outbox message {Id} abandoned after {MaxAttempts} attempts; inspect Error column")]
    private static partial void LogMessageAbandoned(ILogger logger, Guid id, int maxAttempts);
}
