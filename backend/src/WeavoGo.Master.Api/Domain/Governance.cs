namespace WeavoGo.Master.Api.Domain;

/// <summary>SDS §8.2.1.</summary>
public class ApprovalWorkflowTemplate : AuditableEntity
{
    public int WorkflowTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public int? ItemCategoryId { get; set; }

    public ItemCategory? Category { get; set; }
    public ICollection<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
}

/// <summary>SDS §8.2.2.</summary>
public class ApprovalStep : AuditableEntity
{
    public int ApprovalStepId { get; set; }
    public int WorkflowTemplateId { get; set; }
    public int StepOrder { get; set; }
    public string ApproverRole { get; set; } = null!;
    public bool IsMandatory { get; set; } = true;

    public ApprovalWorkflowTemplate Template { get; set; } = null!;
}

/// <summary>SDS §8.2.3.</summary>
public class ItemApprovalRequest : AuditableEntity
{
    public long RequestId { get; set; }
    public long ItemId { get; set; }
    public int WorkflowTemplateId { get; set; }
    public int RequestedBy { get; set; }
    public DateTime RequestedDate { get; set; }
    public int CurrentStepOrder { get; set; } = 1;
    public string OverallStatus { get; set; } = ApprovalStatuses.Pending;

    /// <summary>Pending edit payload for a PATCH against an Active item (SDS §10.6).</summary>
    public int? PendingVersionNumber { get; set; }
    public string? PendingChangeJson { get; set; }
    public string? ChangeReason { get; set; }

    public ItemMaster Item { get; set; } = null!;
    public ApprovalWorkflowTemplate Template { get; set; } = null!;
    public ICollection<ItemApprovalAction> Actions { get; set; } = new List<ItemApprovalAction>();
}

public static class ApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class ApprovalDecisions
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>SDS §8.2.4.</summary>
public class ItemApprovalAction : AuditableEntity
{
    public long ActionId { get; set; }
    public long RequestId { get; set; }
    public int StepOrder { get; set; }
    public int ActionedBy { get; set; }
    public DateTime ActionDate { get; set; }
    public string Decision { get; set; } = null!;
    public string? Comments { get; set; }

    public ItemApprovalRequest Request { get; set; } = null!;
}

/// <summary>SDS §8.3.1 — written exclusively by database triggers; read-only to the API.</summary>
public class ItemAuditLog : AuditableEntity
{
    public long AuditLogId { get; set; }
    public long ItemId { get; set; }
    public string TableName { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangeType { get; set; } = null!;
    public int ChangedBy { get; set; }
    public DateTime ChangedDate { get; set; }
}

/// <summary>SDS §8.4.1 — full snapshot per approved revision.</summary>
public class ItemVersion : AuditableEntity
{
    public long ItemVersionId { get; set; }
    public long ItemId { get; set; }
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string? ChangeReason { get; set; }
    public long? ApprovalRequestId { get; set; }
}

/// <summary>SDS §8.5.1.</summary>
public class ItemObsolescence : AuditableEntity
{
    public int ObsolescenceId { get; set; }
    public long ItemId { get; set; }
    public long? ReplacementItemId { get; set; }
    public DateOnly ObsoleteDate { get; set; }
    public string Reason { get; set; } = null!;
}
