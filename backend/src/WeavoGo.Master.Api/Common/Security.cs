namespace WeavoGo.Master.Api.Common;

/// <summary>Role names exactly as they appear in the SDS §12.3 matrix and ApprovalStep.ApproverRole.</summary>
public static class RoleNames
{
    public const string SystemAdministrator = "System Administrator";
    public const string CategoryManager = "Category Manager";
    public const string FinanceController = "Finance Controller";
    public const string QcManager = "QC Manager";
    public const string WarehouseClerk = "Warehouse Clerk";
    public const string SalesUser = "Sales User";
    public const string ReadOnly = "Read-Only";
    public const string ItSecurityReview = "IT Security Review";
}

/// <summary>
/// Authorization policies, one per row of the SDS §12.3 security matrix.
/// "Scoped" rows are additionally checked against the caller's business units
/// inside the services — a policy alone cannot see the item being acted on.
/// </summary>
public static class Policies
{
    public const string CreateDraftItem = "item:create";
    public const string EditDraftItem = "item:edit";
    public const string SubmitForApproval = "item:submit";
    public const string ApproveStep = "item:approve";
    public const string ViewItem = "item:view";
    public const string MarkObsolete = "item:obsolete";
    public const string ViewAuditAndVersions = "item:audit";
    public const string ManageBusinessUnitMapping = "item:bumapping";
}

public static class ClaimTypesEx
{
    public const string UserId = "uid";
    public const string BusinessUnitIds = "bu";
}
