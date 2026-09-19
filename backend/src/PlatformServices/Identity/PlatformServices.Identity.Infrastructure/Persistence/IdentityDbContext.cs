namespace PlatformServices.Identity.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using PlatformServices.Identity.Domain;
using Weavo.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// EF Core unit of work for the Identity bounded context (SQL Server, per B.4.8).
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>ItemMasterDbContext</c>: the Outbox interceptor drains domain
/// events into <see cref="OutboxMessages"/> inside the business transaction, and
/// inserts save twice so IDENTITY-assigned keys reach any creation event.
/// </para>
/// <para>
/// Identity does not raise creation events today, so the two-phase save is not
/// yet needed here; it will be when user-registration lands. The interceptor
/// covers every other event because they are raised on already-persisted rows.
/// </para>
/// </remarks>
/// <param name="options">Provider and connection configuration.</param>
/// <param name="outboxInterceptor">Drains domain events into the Outbox.</param>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ConvertDomainEventsToOutboxMessagesInterceptor outboxInterceptor) : DbContext(options)
{
    /// <summary>Schema owning every Identity table.</summary>
    public const string SchemaName = "identity";

    /// <summary>Login records (A.12.2).</summary>
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    /// <summary>Authentication state (separate aggregate).</summary>
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    /// <summary>Permission grants (A.12.1).</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Role assignments scoped to a business unit (A.12.3).</summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>Domain events awaiting dispatch.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.AddInterceptors(outboxInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // The Outbox mapping lives in BuildingBlocks, so the assembly scan above
        // does not find it.
        modelBuilder.ApplyConfiguration(
            new OutboxMessageConfiguration(SchemaName, isSqlServer: true));

        base.OnModelCreating(modelBuilder);
    }
}
