namespace ItemMaster.Infrastructure.BackgroundJobs;

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ItemMaster.Infrastructure.Persistence;
using ItemMaster.Infrastructure.Persistence.Outbox;

/// <summary>
/// Polls the Outbox and republishes pending domain events through MediatR.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at-least-once: a crash between publishing and stamping
/// <c>ProcessedOnUtc</c> replays the message on the next pass. Notification
/// handlers must therefore be idempotent, keyed on
/// <c>OutboxMessage.Id</c> (which carries the domain event's own EventId).
/// </para>
/// <para>
/// A failed message is stamped processed with its error recorded, so one
/// poisoned row cannot block the queue behind it. Inspect rows where
/// <c>Error IS NOT NULL</c> to find them.
/// </para>
/// </remarks>
/// <param name="serviceScopeFactory">Creates a scope per polling pass.</param>
/// <param name="logger">Logger for dispatch failures.</param>
public sealed partial class ProcessOutboxMessagesJob(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProcessOutboxMessagesJob> logger) : BackgroundService
{
    private const int BatchSize = 20;

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
                // Normal shutdown, not a failure.
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
                // Shutdown requested during the delay.
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ItemMasterDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null)
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

                var domainEvent = JsonSerializer.Deserialize(message.Content, eventType);

                if (domainEvent is not null)
                {
                    await publisher.Publish(domainEvent, stoppingToken).ConfigureAwait(false);
                }

                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMessageFailed(logger, message.Id, ex);
                message.Error = ex.ToString();
                message.ProcessedOnUtc = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
    }

    // Source-generated log methods, required by CA1848 which
    // Directory.Build.props promotes to an error.

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Could not resolve type {Type} for outbox message {Id}")]
    private static partial void LogTypeResolutionFailed(ILogger logger, string type, Guid id);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Failed to process outbox message {Id}")]
    private static partial void LogMessageFailed(ILogger logger, Guid id, Exception exception);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Error occurred while executing Outbox processing loop")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);
}
