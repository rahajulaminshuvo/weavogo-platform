namespace WeavoGo.Master.Api.Domain;

/// <summary>SDS §8.1 — the item lifecycle states.</summary>
public static class ItemStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Obsolete = "Obsolete";

    /// <summary>Permitted transitions (SDS §8.1). Rejection returns an item to Draft.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [Draft] = new[] { PendingApproval },
        [PendingApproval] = new[] { Active, Draft },
        [Active] = new[] { PendingApproval, Inactive, Obsolete },
        [Inactive] = new[] { Active, Obsolete },
        [Obsolete] = Array.Empty<string>()
    };

    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);
}

/// <summary>SDS §5.4.</summary>
public class Uom : AuditableEntity
{
    public int UOMId { get; set; }
    public string UOMCode { get; set; } = null!;
    public string UOMName { get; set; } = null!;
    public string UOMType { get; set; } = null!;
    public byte DecimalPrecision { get; set; }
}

/// <summary>SDS §4.10 — the transactable item record.</summary>
public class ItemMaster : AuditableEntity
{
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }

    // Denormalized ancestry (SDS §4.2) — kept in sync by TR_ItemMaster_ValidateAncestry.
    public int ItemCategoryId { get; set; }
    public int ItemGroupId { get; set; }
    public int ItemSubGroupId { get; set; }
    public int ItemFamilyId { get; set; }

    public int? AttributeTemplateId { get; set; }
    public int BaseUOMId { get; set; }

    public bool CanPurchase { get; set; } = true;
    public bool CanSell { get; set; }
    public bool CanManufacture { get; set; }
    public bool CanStock { get; set; } = true;
    public bool CanTransfer { get; set; } = true;
    public bool IsSerialControlled { get; set; }
    public bool IsLotControlled { get; set; }

    public string ItemStatus { get; set; } = ItemStatuses.Draft;
    public int VersionNumber { get; set; } = 1;

    public ItemCategory Category { get; set; } = null!;
    public ItemGroup Group { get; set; } = null!;
    public ItemSubGroup SubGroup { get; set; } = null!;
    public ItemFamily Family { get; set; } = null!;
    public AttributeTemplate? AttributeTemplate { get; set; }
    public Uom BaseUOM { get; set; } = null!;

    public ICollection<ItemAttribute> Attributes { get; set; } = new List<ItemAttribute>();
    public ICollection<BusinessUnitItem> BusinessUnitItems { get; set; } = new List<BusinessUnitItem>();
    public ICollection<ItemPackaging> Packaging { get; set; } = new List<ItemPackaging>();
}

/// <summary>SDS §4.11 — one row per item per attribute.</summary>
public class ItemAttribute : AuditableEntity
{
    public long ItemAttributeId { get; set; }
    public long ItemId { get; set; }
    public int AttributeDefinitionId { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateOnly? ValueDate { get; set; }
    public bool? ValueBoolean { get; set; }

    public ItemMaster Item { get; set; } = null!;
    public AttributeDefinition Definition { get; set; } = null!;

    /// <summary>Clears every value column — used before writing a new typed value.</summary>
    public void ClearValues()
    {
        ValueText = null;
        ValueNumber = null;
        ValueDate = null;
        ValueBoolean = null;
    }
}

/// <summary>SDS §5.3 — item-to-business-unit authorization.</summary>
public class BusinessUnitItem : AuditableEntity
{
    public long BusinessUnitItemId { get; set; }
    public long ItemId { get; set; }
    public int BusinessUnitId { get; set; }
    public bool IsAuthorized { get; set; } = true;
    public string? LocalItemCode { get; set; }
    public DateOnly AuthorizedDate { get; set; }

    public ItemMaster Item { get; set; } = null!;
    public BusinessUnit BusinessUnit { get; set; } = null!;
}

/// <summary>SDS §5.6 — the item-specific packaging hierarchy.</summary>
public class ItemPackaging : AuditableEntity
{
    public int ItemPackagingId { get; set; }
    public long ItemId { get; set; }
    public byte PackagingLevel { get; set; }
    public int PackagingUOMId { get; set; }
    public decimal QtyPerParentLevel { get; set; }
    public bool IsPurchaseUOM { get; set; }
    public bool IsSalesUOM { get; set; }

    public ItemMaster Item { get; set; } = null!;
    public Uom PackagingUOM { get; set; } = null!;
}
