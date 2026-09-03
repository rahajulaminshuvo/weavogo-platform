namespace ItemMaster.Application.Items.Handlers;

using MediatR;
using ItemMaster.Application.Abstractions;
using ItemMaster.Application.Items.Commands;
using ItemMaster.Domain.Entities;

/// <summary>
/// Handles the ItemMaster command and query set.
/// </summary>
/// <remarks>
/// Depends on <see cref="IItemMasterRepository"/> rather than on
/// <c>ItemMasterDbContext</c>. Taking the DbContext directly would require the
/// Application project to reference Infrastructure, inverting the Clean
/// Architecture dependency arrow and making these handlers untestable without a
/// persistence provider.
/// </remarks>
/// <param name="repository">Persistence gateway for the aggregate.</param>
/// <param name="currentUser">Resolves the acting user from the request.</param>
public sealed class ItemCommandHandler(
    IItemMasterRepository repository,
    ICurrentUserProvider currentUser) :
    IRequestHandler<CreateItemCommand, ItemCreatedResponse>,
    IRequestHandler<UpdateItemStatusCommand, Unit>,
    IRequestHandler<GetItemByIdQuery, ItemResponse?>
{
    /// <inheritdoc />
    public async Task<ItemCreatedResponse> Handle(
        CreateItemCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUserId = currentUser.GetCurrentUserId();

        // The aggregate's constructor owns every invariant; the handler never
        // assigns state directly.
        var aggregate = new ItemMasterRecord(
            request.ItemCode,
            request.ItemName,
            request.ItemCategoryId,
            request.ItemGroupId,
            request.ItemSubGroupId,
            request.ItemFamilyId,
            request.BaseUOMId,
            currentUserId,
            request.CanPurchase,
            request.CanSell,
            request.CanManufacture);

        repository.Add(aggregate);

        // SaveChanges assigns the BIGINT IDENTITY, then raises and persists
        // ItemCreatedDomainEvent to the Outbox in the same transaction.
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ItemCreatedResponse(aggregate.Id, aggregate.ItemCode, aggregate.ItemStatus);
    }

    /// <inheritdoc />
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no item exists with the requested identity.
    /// </exception>
    public async Task<Unit> Handle(
        UpdateItemStatusCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = await repository
            .GetByIdAsync(request.ItemId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Item with ID {request.ItemId} was not found.");

        item.UpdateStatus(request.NewStatus, currentUser.GetCurrentUserId());

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }

    /// <inheritdoc />
    public async Task<ItemResponse?> Handle(
        GetItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = await repository
            .GetByIdAsync(request.ItemId, cancellationToken)
            .ConfigureAwait(false);

        return item is null
            ? null
            : new ItemResponse(
                item.Id,
                item.ItemCode,
                item.ItemName,
                item.ItemStatus,
                item.BaseUOMId,
                item.CreatedDate);
    }
}
