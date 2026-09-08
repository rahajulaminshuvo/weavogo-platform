using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Weavo.BuildingBlocks.Messaging;

/// <summary>Broker settings, bound from the <c>Messaging</c> section.</summary>
public sealed class MessagingOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Messaging";

    /// <summary>RabbitMQ host, e.g. <c>rabbitmq://localhost/</c>.</summary>
    public string Host { get; set; } = "rabbitmq://localhost/";

    /// <summary>Virtual host, isolating environments on a shared broker.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Broker username.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>Broker password. Supply via user-secrets or the platform vault.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Owning service, e.g. <c>item-master</c>. Prefixes every queue this
    /// service declares so ownership is readable from the broker console.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Immediate retries before a message is scheduled for redelivery.</summary>
    public int RetryLimit { get; set; } = 3;

    /// <summary>Base delay between immediate retries, in seconds.</summary>
    public int RetryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Delayed redelivery intervals in minutes, applied after immediate retries
    /// are exhausted. Spacing them out lets a dependency recover rather than
    /// burning all attempts inside a few seconds of an outage.
    /// </summary>
    public int[] RedeliveryIntervalsMinutes { get; set; } = [1, 5, 15];

    /// <summary>Concurrent messages a single endpoint will process.</summary>
    public int PrefetchCount { get; set; } = 16;
}

/// <summary>
/// Registers MassTransit, the queue topology, and the resilience policy every
/// service shares (B.5.2, B.5.3).
/// </summary>
public static class MessagingConfiguration
{
    /// <summary>
    /// Adds the event bus and configures this service's receive endpoints.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration holding the Messaging section.</param>
    /// <param name="consumerAssembly">
    /// Assembly scanned for <c>IConsumer</c> implementations. Pass the service's
    /// Application assembly; consumers live beside the handlers they feed.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Messaging:ServiceName</c> is missing — queue names are
    /// derived from it, and an unnamed service would collide with every other.
    /// </exception>
    public static IServiceCollection AddWeavoMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly consumerAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(consumerAssembly);

        var section = configuration.GetSection(MessagingOptions.SectionName);

        services.Configure<MessagingOptions>(section);

        var options = section.Get<MessagingOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{MessagingOptions.SectionName}' is missing.");

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            throw new InvalidOperationException(
                "'Messaging:ServiceName' is required: queue names are derived from it.");
        }

        services.AddMassTransit(bus =>
        {
            bus.AddConsumers(consumerAssembly);

            // Queues are named "<service>-<event>" rather than by consumer type.
            // Each service therefore owns one durable queue per event it
            // subscribes to, so a slow consumer in one service can never block
            // delivery to another.
            bus.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(options.ServiceName, includeNamespace: false));

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(options.Host), host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });

                // Required for the delayed redelivery below; RabbitMQ has no
                // native scheduling without the delayed-exchange plugin.
                rabbit.UseDelayedMessageScheduler();

                rabbit.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(
                    options.ServiceName, includeNamespace: false));
            });
        });

        services.AddScoped<IEventBus, MassTransitEventBus>();

        return services;
    }

    /// <summary>
    /// Applies the shared retry, redelivery and concurrency policy to one endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two tiers, deliberately: immediate retries absorb a transient blip
    /// (a dropped connection, a deadlock); delayed redelivery spaces later
    /// attempts out so a dependency outage has time to resolve. Exhausting both
    /// moves the message to <c>&lt;queue&gt;_error</c>, MassTransit's dead-letter
    /// queue, where it waits for inspection rather than being discarded.
    /// </para>
    /// <para>
    /// Call from a service's own endpoint configuration when it needs to
    /// override the defaults; <see cref="AddWeavoMessaging"/> applies them
    /// automatically otherwise.
    /// </para>
    /// </remarks>
    /// <param name="endpoint">The endpoint to configure.</param>
    /// <param name="options">Retry and prefetch settings.</param>
    public static void ApplyWeavoResiliencePolicy(
        this IReceiveEndpointConfigurator endpoint,
        MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);

        endpoint.PrefetchCount = options.PrefetchCount;

        endpoint.UseMessageRetry(retry => retry.Interval(
            options.RetryLimit,
            TimeSpan.FromSeconds(options.RetryIntervalSeconds)));

        endpoint.UseDelayedRedelivery(redelivery => redelivery.Intervals(
            options.RedeliveryIntervalsMinutes
                .Select(minutes => TimeSpan.FromMinutes(minutes))
                .ToArray()));
    }
}
