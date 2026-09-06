namespace ItemMaster.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ItemMaster.Application.Abstractions;
using ItemMaster.Infrastructure.BackgroundJobs;
using ItemMaster.Infrastructure.Persistence;
using ItemMaster.Infrastructure.Persistence.Interceptors;
using ItemMaster.Infrastructure.Persistence.Repositories;

/// <summary>
/// Registers the ItemMaster persistence stack and the Outbox dispatcher.
/// </summary>
/// <remarks>
/// Keeping registration here means the API host composes the service with a
/// single call and never references EF Core directly.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Adds every persistence dependency for this service.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Configuration holding the connection string.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at start-up when the connection string is missing -- deliberately
    /// early, so a misconfigured deployment fails before serving traffic.
    /// </exception>
    public static IServiceCollection AddItemMasterInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        // Stateless and thread-safe, so one instance serves every context.
        services.AddSingleton<ConvertDomainEventsToOutboxMessagesInterceptor>();

        services.AddDbContext<ItemMasterDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(
                    typeof(ItemMasterDbContext).Assembly.GetName().Name);

                // Transient network faults are expected in a containerised
                // deployment. Note that a retrying execution strategy forbids
                // user-initiated transactions unless wrapped in an execution
                // strategy delegate; the DbContext handles its own.
                sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IItemMasterRepository, ItemMasterRepository>();

        // Scoped, like the DbContext it depends on. Resolving it as a singleton
        // would capture a scoped DbContext and outlive its lifetime.
        services.AddScoped<ItemMasterDbContextSeeder>();

        services.AddHostedService<ProcessOutboxMessagesJob>();

        return services;
    }
}
