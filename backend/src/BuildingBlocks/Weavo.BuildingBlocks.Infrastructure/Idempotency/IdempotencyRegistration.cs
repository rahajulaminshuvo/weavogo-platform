using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Weavo.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>
/// Registers the Redis-backed deduplication store every consumer needs (B.5.3).
/// </summary>
public static class IdempotencyRegistration
{
    /// <summary>
    /// Adds <see cref="IIdempotencyStore"/> over the shared Redis Cluster.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration holding the Redis section.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// With no <c>Redis:ConnectionString</c> configured, an in-memory store is
    /// registered instead. That keeps local development and tests running
    /// without a Redis instance, but it deduplicates only within one process —
    /// two replicas would each process the same message once. Configure Redis
    /// for any environment running more than a single instance.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddItemMasterIdempotency(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(RedisIdempotencyOptions.SectionName);
        services.Configure<RedisIdempotencyOptions>(section);

        var options = section.Get<RedisIdempotencyOptions>();

        if (string.IsNullOrWhiteSpace(options?.ConnectionString))
        {
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            return services;
        }

        // IConnectionMultiplexer owns its own connection pool and is thread-safe,
        // so exactly one instance must exist per process. Registering it scoped
        // would open a pool per request and exhaust connections under load.
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(options.ConnectionString));

        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

        return services;
    }
}

/// <summary>
/// Process-local fallback for development and tests.
/// </summary>
/// <remarks>
/// Not suitable for production: state dies with the process and is invisible to
/// other replicas, so a redelivered message reaching a different instance would
/// be processed a second time.
/// </remarks>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly HashSet<string> _claims = [];
    // .NET 8 has no System.Threading.Lock; that type arrives in .NET 9.
    private readonly object _gate = new();

    /// <inheritdoc />
    public Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        lock (_gate)
        {
            return Task.FromResult(_claims.Add($"{consumerName}:{messageId:N}"));
        }
    }

    /// <inheritdoc />
    public Task ReleaseAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        lock (_gate)
        {
            _claims.Remove($"{consumerName}:{messageId:N}");
        }

        return Task.CompletedTask;
    }
}
