namespace ItemMaster.Application.Abstractions;

using ItemMaster.Domain.Entities;

/// <summary>
/// Persistence gateway for the <see cref="ItemMasterRecord"/> aggregate.
/// </summary>
/// <remarks>
/// Declared in the Application layer and implemented in Infrastructure. This is
/// the dependency inversion that lets handlers be unit-tested without a
/// database and keeps EF Core out of the inner layers -- a handler that took
/// ItemMasterDbContext directly would invert the dependency arrow and make the
/// Application layer unbuildable without a persistence provider.
/// </remarks>
public interface IItemMasterRepository
{
    /// <summary>Loads an item by identity, or null when absent or soft-deleted.</summary>
    /// <param name="itemId">Identity to load.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ItemMasterRecord?> GetByIdAsync(
        long itemId,
        CancellationToken cancellationToken = default);

    /// <summary>Stages a new aggregate for insertion.</summary>
    /// <param name="item">The aggregate to add.</param>
    void Add(ItemMasterRecord item);

    /// <summary>Commits all staged changes.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the identity of the user on whose behalf the current request runs.
/// </summary>
/// <remarks>
/// Implemented in the API layer over <c>IHttpContextAccessor</c>. Declared here
/// so handlers depend on the concept rather than on ASP.NET.
/// </remarks>
public interface ICurrentUserProvider
{
    /// <summary>The authenticated user's id.</summary>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when no authenticated user can be resolved.
    /// </exception>
    int GetCurrentUserId();
}
