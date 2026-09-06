namespace ItemMaster.Infrastructure.Persistence;

using ItemMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Applies pending migrations and populates <c>dbo.ItemMaster</c> with baseline
/// data, but only when the table is empty.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent: the emptiness check means a restart against a populated database
/// is a no-op, so this is safe to run on every boot in development.
/// </para>
/// <para>
/// Seed rows are built through <see cref="ItemMasterRecord"/>'s public
/// constructor rather than object initialisers. Every property on the aggregate
/// is <c>private set</c>, so initialisers would not compile - and going through
/// the constructor means seed data is held to the same invariants as anything
/// the API accepts. A typo that violates <c>CK_ItemMaster_CodeFormat</c> fails
/// here rather than at the database.
/// </para>
/// <para>
/// Seeding through the aggregate also means each row raises
/// <c>ItemCreatedDomainEvent</c>, so the Outbox receives a creation event per
/// seeded item exactly as a real API call would.
/// </para>
/// </remarks>
/// <param name="context">The unit of work to seed through.</param>
/// <param name="logger">Logger for seeding progress and failures.</param>
public sealed partial class ItemMasterDbContextSeeder(
    ItemMasterDbContext context,
    ILogger<ItemMasterDbContextSeeder> logger)
{
    /// <summary>User id recorded as the creator of seeded rows.</summary>
    /// <remarks>
    /// A reserved system account, distinct from any real user, so seeded data is
    /// identifiable in the audit trail.
    /// </remarks>
    private const int SystemUserId = 1;

    /// <summary>
    /// Migrates the schema, then inserts baseline items if none exist.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="Exception">
    /// Rethrown after logging. A database that cannot be migrated or seeded is
    /// not a state the application should start serving traffic in.
    /// </exception>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Migrations only apply to a real relational provider; the in-memory
            // provider used by tests has no schema to migrate.
            if (context.Database.IsRelational())
            {
                await context.Database
                    .MigrateAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (await context.ItemMasters.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                LogSeedSkipped(logger);
                return;
            }

            LogSeedStarting(logger);

            var initialItems = BuildInitialItems();

            context.ItemMasters.AddRange(initialItems);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogSeedCompleted(logger, initialItems.Count);
        }
        catch (Exception ex)
        {
            LogSeedFailed(logger, ex);
            throw;
        }
    }

    /// <summary>Baseline catalogue items, one per supply-chain role.</summary>
    private static List<ItemMasterRecord> BuildInitialItems() =>
    [
        // Finished good: bought and sold, not manufactured in-house.
        new ItemMasterRecord(
            itemCode: "LAPTOP-XPS-15",
            itemName: "Dell XPS 15 Laptop 32GB RAM",
            itemCategoryId: 100,
            itemGroupId: 150,
            itemSubGroupId: 155,
            itemFamilyId: 10,
            baseUOMId: 1,
            createdBy: SystemUserId,
            canPurchase: true,
            canSell: true,
            canManufacture: false),

        // Raw material: purchased and consumed by production. CanManufacture
        // forces CanStock true, satisfying CK_ItemMaster_StockFlags.
        new ItemMasterRecord(
            itemCode: "RM-STEEL-001",
            itemName: "Stainless Steel Sheet 2mm",
            itemCategoryId: 200,
            itemGroupId: 210,
            itemSubGroupId: 215,
            itemFamilyId: 20,
            baseUOMId: 5,
            createdBy: SystemUserId,
            canPurchase: true,
            canSell: false,
            canManufacture: true),
    ];

    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Information,
        Message = "ItemMaster already contains data; skipping seed.")]
    private static partial void LogSeedSkipped(ILogger logger);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Information,
        Message = "Seeding initial Item Master data...")]
    private static partial void LogSeedStarting(ILogger logger);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "Item Master seeding completed: {ItemCount} item(s) inserted.")]
    private static partial void LogSeedCompleted(ILogger logger, int itemCount);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Error,
        Message = "An error occurred while seeding the Item Master database.")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception);
}
