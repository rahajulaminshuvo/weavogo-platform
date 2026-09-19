namespace PlatformServices.Identity.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformServices.Identity.Application.Contracts;
using PlatformServices.Identity.Infrastructure.Integration;
using PlatformServices.Identity.Infrastructure.Persistence;
using PlatformServices.Identity.Infrastructure.Persistence.Repositories;
using PlatformServices.Identity.Infrastructure.Security;
using Weavo.BuildingBlocks.Infrastructure.Idempotency;
using Weavo.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Registers Identity's persistence, security and dispatch stack.</summary>
public static class DependencyInjection
{
    /// <summary>Adds every Infrastructure dependency for this service.</summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Configuration holding the required sections.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at start-up when the connection string is missing — deliberately
    /// early, so a misconfigured deployment fails before serving traffic.
    /// </exception>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException(
                "Connection string 'IdentityDb' is not configured.");

        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        services.Configure<OrganizationDefaults>(
            configuration.GetSection(OrganizationDefaults.SectionName));

        // Stateless and thread-safe; shared across contexts per B.3.4.
        services.AddSingleton<ConvertDomainEventsToOutboxMessagesInterceptor>();

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(
                    typeof(IdentityDbContext).Assembly.GetName().Name);

                // Transient faults are expected in a containerised deployment (B.4.7).
                sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IUserCredentialRepository, UserCredentialRepository>();
        services.AddScoped<IUserContextResolver, UserContextResolver>();

        // Both stateless: one instance serves every request.
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Redis dedup keys for consumers (B.5.3). Registered even though Identity
        // only publishes today: it will consume other services' events, and the
        // multiplexer is a singleton either way.
        services.AddWeavoIdempotency(configuration);

        services.AddHostedService<ProcessIdentityOutboxJob>();

        return services;
    }
}
