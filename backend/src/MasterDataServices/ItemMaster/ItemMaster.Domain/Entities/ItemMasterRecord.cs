namespace ItemMaster.Domain.Entities;

using Weavo.BuildingBlocks.Kernel;
using ItemMaster.Domain.Events;

/// <summary>
/// Aggregate root for a catalogue item. Maps to <c>dbo.ItemMaster</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every setter is private; state changes go through behaviour methods that
/// check invariants first and raise a domain event on success. The invariants
/// mirror the table's CHECK constraints, so the domain rejects bad state before
/// the database has to.
/// </para>
/// <para>
/// <b>Identity timing.</b> <c>ItemId</c> is <c>BIGINT IDENTITY</c>, so
/// <see cref="Entity{TId}.Id"/> is 0 until the INSERT completes. The creation
/// event is therefore deferred to <see cref="RaiseCreatedEvent"/>, which the
/// persistence layer calls once the identity exists.
/// </para>
/// </remarks>
public class ItemMasterRecord : AggregateRoot<long>
{
    /// <summary>Statuses permitted by <c>CK_ItemMaster_Status</c>.</summary>
    private static readonly string[] ValidStatuses =
        ["Draft", "PendingApproval", "Active", "Inactive", "Obsolete"];

    private bool _creationPending;

    /// <summary>Business key, uppercase. Unique per <c>UQ_ItemMaster_Code</c>.</summary>
    public string ItemCode { get; private set; } = string.Empty;

    /// <summary>Human-readable item name.</summary>
    public string ItemName { get; private set; } = string.Empty;

    /// <summary>Optional long-form description.</summary>
    public string? Description { get; private set; }

    // Hierarchy ancestry

    /// <summary>Denormalised classification ancestor.</summary>
    public int ItemCategoryId { get; private set; }

    /// <summary>Denormalised classification ancestor.</summary>
    public int ItemGroupId { get; private set; }

    /// <summary>Denormalised classification ancestor.</summary>
    public int ItemSubGroupId { get; private set; }

    /// <summary>Direct parent in the classification hierarchy.</summary>
    public int ItemFamilyId { get; private set; }

    /// <summary>Optional attribute template driving item-specific properties.</summary>
    public int? AttributeTemplateId { get; private set; }

    /// <summary>Stocking unit of measure.</summary>
    public int BaseUOMId { get; private set; }

    // Operational flags

    /// <summary>Whether the item can appear on purchase documents.</summary>
    public bool CanPurchase { get; private set; } = true;

    /// <summary>Whether the item can appear on sales documents.</summary>
    public bool CanSell { get; private set; }

    /// <summary>Whether the item is produced in-house.</summary>
    public bool CanManufacture { get; private set; }

    /// <summary>Whether stock is tracked for the item.</summary>
    public bool CanStock { get; private set; } = true;

    /// <summary>Whether the item can move between locations.</summary>
    public bool CanTransfer { get; private set; } = true;

    /// <summary>Whether individual units carry serial numbers.</summary>
    public bool IsSerialControlled { get; private set; }

    /// <summary>Whether stock is tracked in lots or batches.</summary>
    public bool IsLotControlled { get; private set; }

    // Status and auditing

    /// <summary>Lifecycle state. Constrained by <c>CK_ItemMaster_Status</c>.</summary>
    public string ItemStatus { get; private set; } = "Draft";

    /// <summary>Revision counter. Never below 1.</summary>
    public int VersionNumber { get; private set; } = 1;

    /// <summary>User who created the item.</summary>
    public int CreatedBy { get; private set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedDate { get; private set; } = DateTime.UtcNow;

    /// <summary>User who last modified the item, null if never modified.</summary>
    public int? ModifiedBy { get; private set; }

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTime? ModifiedDate { get; private set; }

    /// <summary>Whether the item is available for use.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Soft-delete marker. Rows are never physically removed.</summary>
    public bool IsDeleted { get; private set; }

    // EF Core constructor
    private ItemMasterRecord() { }

    /// <summary>Creates a new catalogue item.</summary>
    /// <param name="itemCode">Business key; 3-30 chars, alphanumeric and hyphen.</param>
    /// <param name="itemName">Human-readable name.</param>
    /// <param name="itemCategoryId">Denormalised classification ancestor.</param>
    /// <param name="itemGroupId">Denormalised classification ancestor.</param>
    /// <param name="itemSubGroupId">Denormalised classification ancestor.</param>
    /// <param name="itemFamilyId">Direct parent in the hierarchy.</param>
    /// <param name="baseUOMId">Stocking unit of measure.</param>
    /// <param name="createdBy">User creating the item.</param>
    /// <param name="canPurchase">Whether the item can be purchased.</param>
    /// <param name="canSell">Whether the item can be sold.</param>
    /// <param name="canManufacture">Whether the item is produced in-house.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the code or name violates its format rule.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no transactable flag is set, violating
    /// <c>CK_ItemMaster_TransactableFlags</c>.
    /// </exception>
    public ItemMasterRecord(
        string itemCode,
        string itemName,
        int itemCategoryId,
        int itemGroupId,
        int itemSubGroupId,
        int itemFamilyId,
        int baseUOMId,
        int createdBy,
        bool canPurchase = true,
        bool canSell = false,
        bool canManufacture = false)
    {
        ValidateCode(itemCode);
        if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("Item name is required", nameof(itemName));
        if (!canPurchase && !canSell && !canManufacture)
            throw new InvalidOperationException("Item must have at least one transactable flag (Purchase, Sell, or Manufacture).");

        ItemCode = itemCode.ToUpperInvariant();
        ItemName = itemName;
        ItemCategoryId = itemCategoryId;
        ItemGroupId = itemGroupId;
        ItemSubGroupId = itemSubGroupId;
        ItemFamilyId = itemFamilyId;
        BaseUOMId = baseUOMId;
        CreatedBy = createdBy;
        CreatedDate = DateTime.UtcNow;

        CanPurchase = canPurchase;
        CanSell = canSell;
        CanManufacture = canManufacture;

        // CK_ItemMaster_StockFlags: CanManufacture = 1 requires CanStock = 1.
        if (canManufacture)
        {
            CanStock = true;
        }

        // ItemCreatedDomainEvent is NOT raised here. Id is IDENTITY and is still
        // 0 until the INSERT completes, so an event raised now would record
        // ItemId = 0 in the audit trail forever, with no error to reveal it.
        // The persistence layer calls RaiseCreatedEvent() once the id exists.
        _creationPending = true;
    }

    /// <summary>
    /// Raises <see cref="ItemCreatedDomainEvent"/> now that the database has
    /// assigned <see cref="Entity{TId}.Id"/>.
    /// </summary>
    /// <remarks>
    /// Called by <c>ItemMasterDbContext</c> immediately after the insert
    /// completes. Idempotent: a no-op on any aggregate that was not just
    /// inserted, and on a second call.
    /// </remarks>
    public void RaiseCreatedEvent()
    {
        if (!_creationPending)
        {
            return;
        }

        _creationPending = false;

        RaiseDomainEvent(new ItemCreatedDomainEvent(
            Id, ItemCode, ItemName, ItemFamilyId, BaseUOMId, CreatedBy));
    }

    /// <summary>Moves the item to a new lifecycle state.</summary>
    /// <param name="newStatus">Target status; must be one of the permitted values.</param>
    /// <param name="modifiedBy">User making the change.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="newStatus"/> is not permitted by
    /// <c>CK_ItemMaster_Status</c>.
    /// </exception>
    public void UpdateStatus(string newStatus, int modifiedBy)
    {
        if (!ValidStatuses.Contains(newStatus))
            throw new ArgumentException($"Invalid status: {newStatus}", nameof(newStatus));

        var oldStatus = ItemStatus;
        ItemStatus = newStatus;
        ModifiedBy = modifiedBy;
        ModifiedDate = DateTime.UtcNow;

        RaiseDomainEvent(new ItemStatusChangedDomainEvent(Id, ItemCode, oldStatus, newStatus, modifiedBy));
    }

    /// <summary>
    /// Validates the item code against <c>CK_ItemMaster_CodeFormat</c>.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the code is malformed.</exception>
    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3 || code.Length > 30)
            throw new ArgumentException("ItemCode must be between 3 and 30 characters.", nameof(code));

        if (code.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("ItemCode can only contain alphanumeric characters and hyphens.", nameof(code));
    }
}
