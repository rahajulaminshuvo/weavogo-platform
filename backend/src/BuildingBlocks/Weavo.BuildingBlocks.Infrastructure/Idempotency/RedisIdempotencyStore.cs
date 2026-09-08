using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Weavo.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>
/// Guards a consumer against acting twice on the same message (B.5.3).
/// </summary>
/// <remarks>
/// Outbox delivery is at-least-once: the dispatcher can publish and then crash
/// before marking the row processed, so the broker replays it. Consumers must
/// therefore be idempotent rather than assume single delivery.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically claims a message for processing.
    /// </summary>
    /// <param name="messageId">The message's identity, from OutboxMessage.Id.</param>
    /// <param name="consumerName">
    /// Consumer claiming it. Part of the key, so several consumers of the same
    /// event each get their own claim rather than starving one another.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// True when this caller won the claim and should process the message;
    /// false when it was already handled and must be skipped.
    /// </returns>
    Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a claim so the message can be retried.
    /// </summary>
    /// <remarks>
    /// Call this when a consumer fails <i>after</i> claiming. Without it the
    /// claim would persist for its full TTL and the redelivered message would
    /// be skipped as a duplicate — silently dropping work that never completed.
    /// </remarks>
    /// <param name="messageId">The message's identity.</param>
    /// <param name="consumerName">Consumer releasing the claim.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ReleaseAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default);
}

/// <summary>Redis deduplication settings, bound from <c>Redis</c>.</summary>
public sealed class RedisIdempotencyOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// How long a claim survives. Must comfortably exceed the broker's maximum
    /// redelivery window, or a late redelivery would be processed twice; but
    /// short enough that keys expire rather than accumulating forever.
    /// </summary>
    public int DeduplicationTtlHours { get; set; } = 24;

    /// <summary>Key prefix, so several services can share one Redis cluster.</summary>
    public string KeyPrefix { get; set; } = "weavo:dedup";
}

/// <summary>
/// Redis-backed <see cref="IIdempotencyStore"/> using the shared Redis Cluster
/// from B.4.8.
/// </summary>
/// <param name="connectionMultiplexer">Shared, thread-safe Redis connection.</param>
/// <param name="options">Key prefix and TTL settings.</param>
public sealed class RedisIdempotencyStore(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisIdempotencyOptions> options) : IIdempotencyStore
{
    private readonly RedisIdempotencyOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        cancellationToken.ThrowIfCancellationRequested();

        var database = connectionMultiplexer.GetDatabase();

        // SET NX: a single atomic round trip. A GET-then-SET pair would let two
        // concurrent consumers both observe "not present" and both proceed.
        return await database
            .StringSetAsync(
                BuildKey(messageId, consumerName),
                DateTime.UtcNow.ToString("O"),
                TimeSpan.FromHours(_options.DeduplicationTtlHours),
                When.NotExists)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        cancellationToken.ThrowIfCancellationRequested();

        var database = connectionMultiplexer.GetDatabase();

        await database
            .KeyDeleteAsync(BuildKey(messageId, consumerName))
            .ConfigureAwait(false);
    }

    private RedisKey BuildKey(Guid messageId, string consumerName)
        => $"{_options.KeyPrefix}:{consumerName}:{messageId:N}";
}
