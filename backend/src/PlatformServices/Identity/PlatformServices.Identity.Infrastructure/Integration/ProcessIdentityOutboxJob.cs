namespace PlatformServices.Identity.Infrastructure.Integration;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlatformServices.Identity.Infrastructure.Persistence;
using Weavo.BuildingBlocks.Infrastructure.Outbox;
using Weavo.BuildingBlocks.Messaging;

/// <summary>
/// Polls Identity's Outbox and publishes translated events (B.5.3).
/// </summary>
/// <remarks>
/// Same shape as ItemMaster's dispatcher: a row is stamped processed only after
/// the broker confirms, so delivery is at-least-once and consumers deduplicate
/// via <c>IIdempotencyStore</c>. Rows are abandoned after
/// <see cref="MaxAttempts"/> so one poisoned message cannot block the queue.
/// </remarks>
/// <param name="serviceScopeFactory">Creates a scope per polling pass.</param>
/// <param name="logger">Logger for dispatch failures.</param>
public sealed partial class ProcessIdentityOutboxJob(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProcessIdentityOutboxJob> logger) : BackgroundService
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

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
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

                var integrationEvent =
                    IdentityIntegrationEventTranslator.Translate(payload, message);

                if (integrationEvent is null)
                {
                    message.ProcessedOnUtc = DateTime.UtcNow;
                    continue;
                }

                await eventBus
                    .PublishAsync(integrationEvent, stoppingToken)
                    .ConfigureAwait(false);

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
        EventId = 5200,
        Level = LogLevel.Error,
        Message = "Could not resolve type {Type} for outbox message {Id}")]
    private static partial void LogTypeResolutionFailed(ILogger logger, string type, Guid id);

    [LoggerMessage(
        EventId = 5201,
        Level = LogLevel.Error,
        Message = "Failed to publish outbox message {Id} (attempt {Attempt})")]
    private static partial void LogMessageFailed(
        ILogger logger, Guid id, int attempt, Exception exception);

    [LoggerMessage(
        EventId = 5202,
        Level = LogLevel.Error,
        Message = "Error occurred while executing Identity Outbox processing loop")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5203,
        Level = LogLevel.Critical,
        Message = "Outbox message {Id} abandoned after {MaxAttempts} attempts; inspect Error column")]
    private static partial void LogMessageAbandoned(ILogger logger, Guid id, int maxAttempts);
}
