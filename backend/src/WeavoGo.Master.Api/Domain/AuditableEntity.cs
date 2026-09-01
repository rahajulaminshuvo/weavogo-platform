namespace WeavoGo.Master.Api.Domain;

/// <summary>
/// The standard audit footprint applied to every table in the specification (SDS §4.1).
/// </summary>
public abstract class AuditableEntity
{
    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    /// <summary>SQL Server ROWVERSION — optimistic concurrency token (SDS §4.1).</summary>
    public byte[]? RowVersion { get; set; }
}
