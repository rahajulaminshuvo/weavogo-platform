namespace WeavoGo.Master.Api.Domain;

/// <summary>SDS §6.1.1.</summary>
public class Warehouse : AuditableEntity
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public int BusinessUnitId { get; set; }
    public string WarehouseType { get; set; } = null!;
    public string? Address { get; set; }

    public BusinessUnit BusinessUnit { get; set; } = null!;
}

/// <summary>SDS §6.1.2 — the cached stock position; never written by this API (§12.1 rule 24).</summary>
public class WarehouseItem : AuditableEntity
{
    public long WarehouseItemId { get; set; }
    public long ItemId { get; set; }
    public int WarehouseId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public string? BinLocation { get; set; }
    public DateOnly? LastCountDate { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
}

/// <summary>SDS §7.1.1.</summary>
public class QCParameterTemplate : AuditableEntity
{
    public int QCTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<QCParameter> Parameters { get; set; } = new List<QCParameter>();
}

/// <summary>SDS §7.1.2.</summary>
public class QCParameter : AuditableEntity
{
    public int QCParameterId { get; set; }
    public int QCTemplateId { get; set; }
    public string ParameterName { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? TestMethod { get; set; }
    public bool IsCritical { get; set; } = true;

    public QCParameterTemplate Template { get; set; } = null!;
}

/// <summary>SDS §7.1.3 — QC template assigned per family (default) or per item (override).</summary>
public class ItemQCProfile : AuditableEntity
{
    public int ItemQCProfileId { get; set; }
    public int? ItemFamilyId { get; set; }
    public long? ItemId { get; set; }
    public int QCTemplateId { get; set; }
    public bool IsMandatory { get; set; } = true;
    public string InspectionFrequency { get; set; } = "EveryBatch";

    public QCParameterTemplate Template { get; set; } = null!;
}
