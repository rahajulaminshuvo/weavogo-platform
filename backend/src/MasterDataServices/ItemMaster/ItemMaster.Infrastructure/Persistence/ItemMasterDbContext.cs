namespace ItemMaster.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ItemMaster.Domain.Entities;
using ItemMaster.Infrastructure.Persistence.Outbox;
using ItemMaster.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core unit of work for the ItemMaster bounded context.
/// </summary>
/// <remarks>
/// Domain events are moved to the Outbox by
/// <see cref="ConvertDomainEventsToOutboxMessagesInterceptor"/> inside the same
/// transaction as the business write.
/// </remarks>
public sealed class ItemMasterDbContext : DbContext
{
    private readonly ConvertDomainEventsToOutboxMessagesInterceptor _outboxInterceptor;

    /// <summary>Creates the context.</summary>
    /// <param name="options">Provider and connection configuration.</param>
    /// <param name="outboxInterceptor">Interceptor that drains domain events.</param>
    public ItemMasterDbContext(
        DbContextOptions<ItemMasterDbContext> options,
        ConvertDomainEventsToOutboxMessagesInterceptor outboxInterceptor)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(outboxInterceptor);
        _outboxInterceptor = outboxInterceptor;
    }

    /// <summary>Catalogue items owned by this context.</summary>
    public DbSet<ItemMasterRecord> ItemMasters => Set<ItemMasterRecord>();

    /// <summary>Domain events awaiting dispatch.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.AddInterceptors(_outboxInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ItemMasterDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Saves twice when new items were inserted. <c>ItemId</c> is IDENTITY, so
    /// it does not exist until the first INSERT completes; only then can
    /// <c>ItemCreatedDomainEvent</c> be raised carrying a real id. The second
    /// save writes just those Outbox rows.
    /// <para>
    /// Both saves run inside one explicit transaction, so an item can never be
    /// committed without the event announcing it. Providers without transaction
    /// support (the in-memory provider used in tests) fall back to two saves.
    /// </para>
    /// </remarks>
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var insertedItems = CollectInsertedItems();

        if (insertedItems.Count == 0)
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Database.CurrentTransaction is not null || !Database.IsRelational())
        {
            // Already inside a caller-owned transaction, or a provider that does
            // not support them. Do not open a nested one.
            return await SaveInsertedItemsAsync(insertedItems, cancellationToken)
                .ConfigureAwait(false);
        }

        var transaction = await Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (transaction.ConfigureAwait(false))
        {
            var affected = await SaveInsertedItemsAsync(insertedItems, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return affected;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Mirrors <see cref="SaveChangesAsync"/>; see its remarks for why the save
    /// happens twice.
    /// </remarks>
    public override int SaveChanges()
    {
        var insertedItems = CollectInsertedItems();

        if (insertedItems.Count == 0)
        {
            return base.SaveChanges();
        }

        if (Database.CurrentTransaction is not null || !Database.IsRelational())
        {
            return SaveInsertedItems(insertedItems);
        }

        using var transaction = Database.BeginTransaction();

        var affected = SaveInsertedItems(insertedItems);

        transaction.Commit();

        return affected;
    }

    private async Task<int> SaveInsertedItemsAsync(
        List<ItemMasterRecord> insertedItems,
        CancellationToken cancellationToken)
    {
        var affected = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!RaiseCreatedEvents(insertedItems))
        {
            return affected;
        }

        // Second pass: the interceptor now sees the creation events, whose
        // ItemId is populated because the INSERT above assigned it.
        return affected + await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private int SaveInsertedItems(List<ItemMasterRecord> insertedItems)
    {
        var affected = base.SaveChanges();

        if (!RaiseCreatedEvents(insertedItems))
        {
            return affected;
        }

        return affected + base.SaveChanges();
    }

    /// <summary>
    /// Captures items being inserted, before the save clears their Added state.
    /// </summary>
    private List<ItemMasterRecord> CollectInsertedItems()
        => ChangeTracker
            .Entries<ItemMasterRecord>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

    /// <summary>
    /// Raises the deferred creation event on each newly inserted item.
    /// </summary>
    /// <param name="insertedItems">Items that were just inserted.</param>
    /// <returns>True when at least one event was raised and needs saving.</returns>
    private static bool RaiseCreatedEvents(List<ItemMasterRecord> insertedItems)
    {
        foreach (var item in insertedItems)
        {
            item.RaiseCreatedEvent();
        }

        return insertedItems.Exists(item => item.DomainEvents.Count > 0);
    }
}
