using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Domain;
using WeavoGo.Master.Api.Infrastructure;

namespace WeavoGo.Master.Api.Services;

public interface IApprovalService
{
    Task<ApprovalStateResponse> SubmitAsync(long itemId, SubmitItemRequest request, CancellationToken ct);
    Task<ApprovalStateResponse> RecordActionAsync(long requestId, ApprovalActionRequest request, CancellationToken ct);
    Task<ApprovalStateResponse> RecordActionForItemAsync(long itemId, ApprovalActionRequest request, CancellationToken ct);
    Task<ApprovalStateResponse> GetAsync(long requestId, CancellationToken ct);
}

/// <summary>
/// Implements the SDS §8.2 workflow: an item reaches Active only by walking every
/// mandatory ApprovalStep in order, each signed off by a user holding that step's role.
/// </summary>
public sealed class ApprovalService : IApprovalService
{
    private readonly ItemMasterDbContext _db;
    private readonly IItemService _items;
    private readonly IAttributeResolver _attributes;
    private readonly ICurrentUser _user;

    public ApprovalService(ItemMasterDbContext db, IItemService items, IAttributeResolver attributes, ICurrentUser user)
    {
        _db = db;
        _items = items;
        _attributes = attributes;
        _user = user;
    }

    // ---------------------------------------------------------------- submit
    public async Task<ApprovalStateResponse> SubmitAsync(long itemId, SubmitItemRequest request, CancellationToken ct)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
                   ?? throw ApiException.NotFound($"Item {itemId} was not found.");

        if (!_user.IsSystemAdmin && !_user.HasRole(RoleNames.CategoryManager))
            throw ApiException.Forbidden(ErrorCodes.Forbidden,
                "Only a Category Manager may submit an item for approval (SDS 12.3).");

        if (!ItemStatuses.CanTransition(item.ItemStatus, ItemStatuses.PendingApproval))
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"An item in status '{item.ItemStatus}' cannot be submitted for approval (SDS 8.1).");

        // SDS §12.1 rule 7 — required attributes must all be present before approval starts.
        await _attributes.AssertRequiredAttributesPresentAsync(item, ct);

        if (await _db.ItemApprovalRequests.AnyAsync(
                r => r.ItemId == itemId && r.OverallStatus == ApprovalStatuses.Pending, ct))
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                "An approval request is already open for this item.");

        var workflowTemplate = await ResolveWorkflowAsync(item, request.WorkflowTemplateCode, ct);
        var firstStep = workflowTemplate.Steps.Where(s => s.IsActive).OrderBy(s => s.StepOrder).First();

        var approval = new ItemApprovalRequest
        {
            ItemId = itemId,
            WorkflowTemplateId = workflowTemplate.WorkflowTemplateId,
            RequestedBy = _user.UserId,
            RequestedDate = DateTime.UtcNow,
            CurrentStepOrder = firstStep.StepOrder,
            OverallStatus = ApprovalStatuses.Pending,
            CreatedBy = _user.UserId
        };
        _db.ItemApprovalRequests.Add(approval);

        item.ItemStatus = ItemStatuses.PendingApproval;
        item.ModifiedBy = _user.UserId;
        item.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApprovalStateResponse
        {
            RequestId = approval.RequestId,
            CurrentStepOrder = approval.CurrentStepOrder,
            CurrentStepRole = firstStep.ApproverRole,
            OverallStatus = approval.OverallStatus,
            ItemStatus = item.ItemStatus
        };
    }

    // ----------------------------------------------------- approve / reject
    public async Task<ApprovalStateResponse> RecordActionForItemAsync(long itemId, ApprovalActionRequest request,
                                                                      CancellationToken ct)
    {
        var open = await _db.ItemApprovalRequests
            .Where(r => r.ItemId == itemId && r.OverallStatus == ApprovalStatuses.Pending)
            .OrderByDescending(r => r.RequestId)
            .FirstOrDefaultAsync(ct)
            ?? throw ApiException.NotFound($"No open approval request exists for item {itemId}.");

        return await RecordActionAsync(open.RequestId, request, ct);
    }

    public async Task<ApprovalStateResponse> RecordActionAsync(long requestId, ApprovalActionRequest request,
                                                               CancellationToken ct)
    {
        if (request.Decision != ApprovalDecisions.Approved && request.Decision != ApprovalDecisions.Rejected)
            throw ApiException.Validation("Decision must be 'Approved' or 'Rejected'.", "decision");

        // §8.2.4 — comments are mandatory on rejection.
        if (request.Decision == ApprovalDecisions.Rejected && string.IsNullOrWhiteSpace(request.Comments))
            throw ApiException.Validation("Comments are required when rejecting.", "comments");

        var approval = await _db.ItemApprovalRequests
            .Include(r => r.Template).ThenInclude(t => t.Steps)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
            ?? throw ApiException.NotFound($"Approval request {requestId} was not found.");

        if (approval.OverallStatus != ApprovalStatuses.Pending)
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"Approval request {requestId} is already {approval.OverallStatus}.");

        if (request.StepOrder != approval.CurrentStepOrder)
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"Step {request.StepOrder} is not the current step ({approval.CurrentStepOrder}).", "stepOrder");

        var steps = approval.Template.Steps.Where(s => s.IsActive).OrderBy(s => s.StepOrder).ToList();
        var currentStep = steps.FirstOrDefault(s => s.StepOrder == approval.CurrentStepOrder)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                $"Step {approval.CurrentStepOrder} is not defined on workflow {approval.Template.TemplateCode}.");

        // SDS §12.1 rule 22 / §12.2 APPROVAL_ROLE_MISMATCH.
        if (!_user.IsSystemAdmin && !_user.HasRole(currentStep.ApproverRole))
            throw ApiException.Forbidden(ErrorCodes.ApprovalRoleMismatch,
                $"This step requires the '{currentStep.ApproverRole}' role.");

        var item = await _db.Items.Include(i => i.Attributes)
            .FirstOrDefaultAsync(i => i.ItemId == approval.ItemId, ct)
            ?? throw ApiException.NotFound($"Item {approval.ItemId} was not found.");

        _db.ItemApprovalActions.Add(new ItemApprovalAction
        {
            RequestId = approval.RequestId,
            StepOrder = currentStep.StepOrder,
            ActionedBy = _user.UserId,
            ActionDate = DateTime.UtcNow,
            Decision = request.Decision,
            Comments = request.Comments,
            CreatedBy = _user.UserId
        });

        string? currentRole = currentStep.ApproverRole;

        if (request.Decision == ApprovalDecisions.Rejected)
        {
            // §8.1 — a rejection returns the item to Draft for correction.
            approval.OverallStatus = ApprovalStatuses.Rejected;
            item.ItemStatus = ItemStatuses.Draft;
            currentRole = null;
        }
        else
        {
            // Skip any following non-mandatory step that nobody can action (§8.2.2).
            var next = steps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder && s.IsMandatory);

            if (next is not null)
            {
                approval.CurrentStepOrder = next.StepOrder;
                currentRole = next.ApproverRole;
            }
            else
            {
                await CompleteApprovalAsync(approval, item, ct);
                currentRole = null;
            }
        }

        approval.ModifiedBy = _user.UserId;
        approval.ModifiedDate = DateTime.UtcNow;
        item.ModifiedBy = _user.UserId;
        item.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApprovalStateResponse
        {
            RequestId = approval.RequestId,
            CurrentStepOrder = approval.CurrentStepOrder,
            CurrentStepRole = currentRole,
            OverallStatus = approval.OverallStatus,
            ItemStatus = item.ItemStatus
        };
    }

    public async Task<ApprovalStateResponse> GetAsync(long requestId, CancellationToken ct)
    {
        var approval = await _db.ItemApprovalRequests.AsNoTracking()
            .Include(r => r.Template).ThenInclude(t => t.Steps)
            .Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
            ?? throw ApiException.NotFound($"Approval request {requestId} was not found.");

        return new ApprovalStateResponse
        {
            RequestId = approval.RequestId,
            CurrentStepOrder = approval.CurrentStepOrder,
            CurrentStepRole = approval.Template.Steps
                .FirstOrDefault(s => s.StepOrder == approval.CurrentStepOrder)?.ApproverRole,
            OverallStatus = approval.OverallStatus,
            ItemStatus = approval.Item.ItemStatus
        };
    }

    /// <summary>
    /// SDS §8.2.3 — the item goes Active in the same transaction as the final approval,
    /// and §8.4.1 — the newly-live state is snapshotted as a version row.
    /// </summary>
    private async Task CompleteApprovalAsync(ItemApprovalRequest approval, ItemMaster item, CancellationToken ct)
    {
        // Apply a pending PATCH payload (§10.6) before the item goes live again.
        if (!string.IsNullOrWhiteSpace(approval.PendingChangeJson))
        {
            var change = JsonSerializer.Deserialize<PatchItemRequest>(approval.PendingChangeJson);
            if (change is not null)
            {
                if (change.ItemName is { Length: > 0 }) item.ItemName = change.ItemName;
                if (change.Description is not null) item.Description = change.Description;
                if (change.ItemCode is { Length: > 0 }) item.ItemCode = change.ItemCode;
                if (change.CanPurchase.HasValue) item.CanPurchase = change.CanPurchase.Value;
                if (change.CanSell.HasValue) item.CanSell = change.CanSell.Value;
                if (change.CanManufacture.HasValue) item.CanManufacture = change.CanManufacture.Value;
                if (change.CanStock.HasValue) item.CanStock = change.CanStock.Value;
                if (change.CanTransfer.HasValue) item.CanTransfer = change.CanTransfer.Value;
                if (change.IsSerialControlled.HasValue) item.IsSerialControlled = change.IsSerialControlled.Value;
                if (change.IsLotControlled.HasValue) item.IsLotControlled = change.IsLotControlled.Value;

                if (change.Attributes is { Count: > 0 })
                    await _attributes.ApplyAttributesAsync(item, change.Attributes, _user.UserId, ct);
            }
        }

        await _attributes.AssertRequiredAttributesPresentAsync(item, ct);

        approval.OverallStatus = ApprovalStatuses.Approved;

        var isRevision = approval.PendingVersionNumber.HasValue;
        if (isRevision) item.VersionNumber = approval.PendingVersionNumber!.Value;

        item.ItemStatus = ItemStatuses.Active;

        // Persist the item change before snapshotting so the snapshot reflects the live row.
        await _db.SaveChangesAsync(ct);

        var snapshot = await _items.BuildSnapshotJsonAsync(item.ItemId, ct);

        var alreadyVersioned = await _db.ItemVersions
            .AnyAsync(v => v.ItemId == item.ItemId && v.VersionNumber == item.VersionNumber, ct);

        if (!alreadyVersioned)
        {
            _db.ItemVersions.Add(new ItemVersion
            {
                ItemId = item.ItemId,
                VersionNumber = item.VersionNumber,
                SnapshotJson = snapshot,
                ChangeReason = approval.ChangeReason ?? (isRevision ? "Approved revision" : "Initial approval and activation"),
                ApprovalRequestId = approval.RequestId,
                CreatedBy = _user.UserId
            });
        }
    }

    private async Task<ApprovalWorkflowTemplate> ResolveWorkflowAsync(ItemMaster item, string? code, CancellationToken ct)
    {
        var query = _db.ApprovalWorkflowTemplates.Include(w => w.Steps).AsQueryable();

        ApprovalWorkflowTemplate? workflow = null;
        if (!string.IsNullOrWhiteSpace(code))
            workflow = await query.FirstOrDefaultAsync(w => w.TemplateCode == code && w.IsActive, ct)
                ?? throw ApiException.Validation($"Approval workflow '{code}' was not found.", "workflowTemplateCode");

        workflow ??= await query.FirstOrDefaultAsync(w => w.ItemCategoryId == item.ItemCategoryId && w.IsActive, ct);
        workflow ??= await query.FirstOrDefaultAsync(w => w.TemplateCode == "STANDARD-ITEM" && w.IsActive, ct);

        if (workflow is null || !workflow.Steps.Any(s => s.IsActive))
            throw ApiException.Unprocessable(ErrorCodes.ValidationFailed,
                "No approval workflow with at least one step is configured for this item.");

        return workflow;
    }
}
