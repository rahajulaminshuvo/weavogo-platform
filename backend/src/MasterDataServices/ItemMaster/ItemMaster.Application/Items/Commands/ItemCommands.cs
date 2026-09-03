namespace ItemMaster.Application.Items.Commands;

using MediatR;

/// <summary>Registers a new item in the catalogue.</summary>
/// <param name="ItemCode">Business key; 3-30 chars, alphanumeric and hyphen.</param>
/// <param name="ItemName">Human-readable name.</param>
/// <param name="ItemCategoryId">Denormalised classification ancestor.</param>
/// <param name="ItemGroupId">Denormalised classification ancestor.</param>
/// <param name="ItemSubGroupId">Denormalised classification ancestor.</param>
/// <param name="ItemFamilyId">Direct parent in the hierarchy.</param>
/// <param name="BaseUOMId">Stocking unit of measure.</param>
/// <param name="CanPurchase">Whether the item can be purchased.</param>
/// <param name="CanSell">Whether the item can be sold.</param>
/// <param name="CanManufacture">Whether the item is produced in-house.</param>
/// <remarks>
/// The creating user is resolved from the caller's token, never taken from the
/// request body: a client must not be able to attribute a change to someone else.
/// </remarks>
public sealed record CreateItemCommand(
    string ItemCode,
    string ItemName,
    int ItemCategoryId,
    int ItemGroupId,
    int ItemSubGroupId,
    int ItemFamilyId,
    int BaseUOMId,
    bool CanPurchase = true,
    bool CanSell = false,
    bool CanManufacture = false
) : IRequest<ItemCreatedResponse>;

/// <summary>Moves an item to a new lifecycle state.</summary>
/// <param name="ItemId">Item to transition.</param>
/// <param name="NewStatus">Target status.</param>
public sealed record UpdateItemStatusCommand(
    long ItemId,
    string NewStatus
) : IRequest<Unit>;

/// <summary>Retrieves a single item by identity.</summary>
/// <param name="ItemId">Identity to load.</param>
public sealed record GetItemByIdQuery(long ItemId) : IRequest<ItemResponse?>;

/// <summary>Identity and state of a newly created item.</summary>
/// <param name="ItemId">Database identity assigned by the insert.</param>
/// <param name="ItemCode">The normalised, uppercase code actually stored.</param>
/// <param name="Status">Lifecycle state; "Draft" for a new item.</param>
public sealed record ItemCreatedResponse(long ItemId, string ItemCode, string Status);

/// <summary>Read model returned for a single item.</summary>
/// <param name="ItemId">Database identity.</param>
/// <param name="ItemCode">Business key.</param>
/// <param name="ItemName">Human-readable name.</param>
/// <param name="ItemStatus">Lifecycle state.</param>
/// <param name="BaseUOMId">Stocking unit of measure.</param>
/// <param name="CreatedDate">UTC creation timestamp.</param>
public sealed record ItemResponse(
    long ItemId,
    string ItemCode,
    string ItemName,
    string ItemStatus,
    int BaseUOMId,
    DateTime CreatedDate
);
