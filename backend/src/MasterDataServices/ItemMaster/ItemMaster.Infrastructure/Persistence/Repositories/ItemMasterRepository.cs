namespace ItemMaster.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ItemMaster.Application.Abstractions;
using ItemMaster.Domain.Entities;

/// <summary>
/// EF Core implementation of <see cref="IItemMasterRepository"/>.
/// </summary>
/// <param name="dbContext">The unit of work for this request.</param>
public sealed class ItemMasterRepository(ItemMasterDbContext dbContext) : IItemMasterRepository
{
    /// <inheritdoc />
    public async Task<ItemMasterRecord?> GetByIdAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        // Tracked deliberately: the caller may mutate the aggregate and needs
        // change detection plus the concurrency token. Soft-deleted rows are
        // excluded so a deleted item behaves as absent.
        return await dbContext.ItemMasters
            .FirstOrDefaultAsync(
                item => item.Id == itemId && !item.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(ItemMasterRecord item)
    {
        ArgumentNullException.ThrowIfNull(item);
        dbContext.ItemMasters.Add(item);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
