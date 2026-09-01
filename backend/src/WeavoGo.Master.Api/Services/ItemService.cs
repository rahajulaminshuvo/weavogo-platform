using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Domain;
using WeavoGo.Master.Api.Infrastructure;

namespace WeavoGo.Master.Api.Services;

public interface IItemService
{
    Task<CreateItemResponse> CreateAsync(CreateItemRequest request, CancellationToken ct);
    Task<ItemDetailResponse> GetAsync(long itemId, CancellationToken ct);
    Task<ItemSearchResponse> SearchAsync(string? categoryCode, string? status, int? businessEntityId,
                                         string? search, int page, int pageSize, CancellationToken ct);
    Task<PatchItemResponse> PatchAsync(long itemId, PatchItemRequest request, CancellationToken ct);
    Task<IReadOnlyList<ItemVersionDto>> GetVersionsAsync(long itemId, CancellationToken ct);
    Task<ItemDetailResponse> AuthorizeBusinessUnitsAsync(long itemId, AuthorizeBusinessUnitsRequest request, CancellationToken ct);
    Task<string> BuildSnapshotJsonAsync(long itemId, CancellationToken ct);
}

public sealed class ItemService : IItemService
{
    private const int MaxPageSize = 100;   // SDS §10.5

    private static readonly string[] PackagingLevelNames = { "", "Each", "Inner Pack", "Outer/Carton", "Pallet" };

    private readonly ItemMasterDbContext _db;
    private readonly IAttributeResolver _attributes;
    private readonly ICurrentUser _user;

    public ItemService(ItemMasterDbContext db, IAttributeResolver attributes, ICurrentUser user)
    {
        _db = db;
        _attributes = attributes;
        _user = user;
    }

    // ---------------------------------------------------------------- create
    public async Task<CreateItemResponse> CreateAsync(CreateItemRequest request, CancellationToken ct)
    {
        ValidateFlags(request.CanPurchase, request.CanSell, request.CanManufacture, request.CanStock);

        if (await _db.Items.AnyAsync(i => i.ItemCode == request.ItemCode, ct))
            throw ApiException.Conflict(ErrorCodes.ItemCodeDuplicate,
                $"Item code '{request.ItemCode}' already exists.", "itemCode");

        var family = await _db.ItemFamilies
            .Include(f => f.SubGroup).ThenInclude(sg => sg.Group)
            .FirstOrDefaultAsync(f => f.ItemFamilyId == request.ItemFamilyId, ct)
            ?? throw ApiException.Validation($"Item family {request.ItemFamilyId} was not found.", "itemFamilyId");

        if (!await _db.Uoms.AnyAsync(u => u.UOMId == request.BaseUOMId, ct))
            throw ApiException.Validation($"Base UOM {request.BaseUOMId} was not found.", "baseUOMId");

        // SDS §12.3 — Create Draft item is scoped to the caller's own business units.
        if (request.BusinessUnitIds.Count > 0 && !_user.IsInScope(request.BusinessUnitIds))
            throw ApiException.Forbidden(ErrorCodes.ItemNotAuthorizedForEntity,
                "One or more requested business units are outside your scope.");

        var item = new ItemMaster
        {
            ItemCode = request.ItemCode,
            ItemName = request.ItemName,
            Description = request.Description,
            // SDS §4.2 — denormalized ancestry, derived rather than trusted from the caller.
            ItemFamilyId = family.ItemFamilyId,
            ItemSubGroupId = family.ItemSubGroupId,
            ItemGroupId = family.SubGroup.ItemGroupId,
            ItemCategoryId = family.SubGroup.Group.ItemCategoryId,
            AttributeTemplateId = request.AttributeTemplateId,
            BaseUOMId = request.BaseUOMId,
            CanPurchase = request.CanPurchase,
            CanSell = request.CanSell,
            CanManufacture = request.CanManufacture,
            CanStock = request.CanStock,
            CanTransfer = request.CanTransfer,
            IsSerialControlled = request.IsSerialControlled,
            IsLotControlled = request.IsLotControlled,
            ItemStatus = ItemStatuses.Draft,      // §8.1 — every item starts in Draft
            VersionNumber = 1,
            CreatedBy = _user.UserId
        };

        // Fails fast with a clear message rather than leaving the DB trigger to reject it.
        await _attributes.ResolveTemplateIdAsync(item, ct);

        _db.Items.Add(item);
        await _db.SaveChangesAsync(ct);

        if (request.Attributes.Count > 0)
        {
            await _attributes.ApplyAttributesAsync(item, request.Attributes, _user.UserId, ct);
            await _db.SaveChangesAsync(ct);
        }

        foreach (var unitId in request.BusinessUnitIds.Distinct())
        {
            _db.BusinessUnitItems.Add(new BusinessUnitItem
            {
                ItemId = item.ItemId,
                BusinessUnitId = unitId,
                IsAuthorized = true,
                AuthorizedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedBy = _user.UserId
            });
        }
        if (request.BusinessUnitIds.Count > 0) await _db.SaveChangesAsync(ct);

        return new CreateItemResponse
        {
            ItemId = item.ItemId,
            ItemCode = item.ItemCode,
            ItemStatus = item.ItemStatus,
            VersionNumber = item.VersionNumber,
            CreatedBy = item.CreatedBy,
            CreatedDate = item.CreatedDate,
            Links =
            {
                ["self"] = $"/api/v1/items/{item.ItemId}",
                ["submitForApproval"] = $"/api/v1/items/{item.ItemId}/submit"
            }
        };
    }

    // ------------------------------------------------------------------- get
    public async Task<ItemDetailResponse> GetAsync(long itemId, CancellationToken ct)
    {
        var item = await LoadItemGraphAsync(itemId, ct);
        AssertReadScope(item);

        var templateId = await _attributes.ResolveTemplateIdAsync(item, ct);
        var templateAttributes = await _attributes.GetTemplateAttributesAsync(templateId, ct);
        var values = item.Attributes.ToDictionary(a => a.AttributeDefinitionId);

        var response = new ItemDetailResponse
        {
            ItemId = item.ItemId,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            Description = item.Description,
            ItemStatus = item.ItemStatus,
            VersionNumber = item.VersionNumber,
            BaseUom = item.BaseUOM.UOMCode,
            Classification = new ItemDetailResponse.ClassificationDto
            {
                CategoryId = item.ItemCategoryId,
                CategoryName = item.Category.CategoryName,
                GroupId = item.ItemGroupId,
                GroupName = item.Group.GroupName,
                SubGroupId = item.ItemSubGroupId,
                SubGroupName = item.SubGroup.SubGroupName,
                FamilyId = item.ItemFamilyId,
                FamilyName = item.Family.FamilyName
            },
            Flags = new ItemDetailResponse.FlagsDto
            {
                CanPurchase = item.CanPurchase,
                CanSell = item.CanSell,
                CanManufacture = item.CanManufacture,
                CanStock = item.CanStock,
                CanTransfer = item.CanTransfer,
                IsSerialControlled = item.IsSerialControlled,
                IsLotControlled = item.IsLotControlled
            }
        };

        foreach (var ta in templateAttributes)
        {
            values.TryGetValue(ta.AttributeDefinitionId, out var value);
            response.Attributes.Add(new ResolvedAttributeDto
            {
                AttributeCode = ta.Definition.AttributeCode,
                AttributeName = ta.Definition.AttributeName,
                DataType = ta.Definition.DataType,
                UnitOfMeasure = ta.Definition.UnitOfMeasure,
                IsRequired = ta.IsRequired,
                DisplayOrder = ta.DisplayOrder,
                Value = ToScalar(value)
            });
        }

        response.BusinessUnits.AddRange(item.BusinessUnitItems.Select(bui => new ItemDetailResponse.BusinessUnitDto
        {
            BusinessUnitId = bui.BusinessUnitId,
            UnitName = bui.BusinessUnit.UnitName,
            IsAuthorized = bui.IsAuthorized
        }));

        response.Packaging.AddRange(item.Packaging.OrderBy(p => p.PackagingLevel)
            .Select(p => new ItemDetailResponse.PackagingDto
            {
                Level = p.PackagingLevel,
                LevelName = PackagingLevelNames[Math.Min(p.PackagingLevel, (byte)4)],
                Uom = p.PackagingUOM.UOMCode,
                QtyPerParentLevel = p.QtyPerParentLevel,
                IsPurchaseUom = p.IsPurchaseUOM,
                IsSalesUom = p.IsSalesUOM
            }));

        // SDS §7.1.3 — item-level QC profile overrides the family-level one.
        var qc = await _db.ItemQCProfiles.Include(p => p.Template)
            .Where(p => p.ItemId == itemId || p.ItemFamilyId == item.ItemFamilyId)
            .OrderByDescending(p => p.ItemId != null)
            .FirstOrDefaultAsync(ct);

        if (qc is not null)
            response.QcProfile = new ItemDetailResponse.QcProfileDto
            {
                QcTemplateId = qc.QCTemplateId,
                TemplateName = qc.Template.TemplateName,
                IsMandatory = qc.IsMandatory,
                InspectionFrequency = qc.InspectionFrequency,
                Source = qc.ItemId != null ? "Item" : "Family"
            };

        return response;
    }

    // ---------------------------------------------------------------- search
    public async Task<ItemSearchResponse> SearchAsync(string? categoryCode, string? status, int? businessEntityId,
                                                      string? search, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? Math.Min(Math.Max(pageSize, 1), MaxPageSize) : pageSize;

        var query = _db.Items.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.BaseUOM)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(categoryCode))
            query = query.Where(i => i.Category.CategoryCode == categoryCode);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.ItemStatus == status);

        if (businessEntityId is > 0)
            query = query.Where(i => _db.BusinessUnitItems.Any(b =>
                b.ItemId == i.ItemId && b.BusinessUnitId == businessEntityId && b.IsAuthorized));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(i => EF.Functions.Like(i.ItemCode, term) || EF.Functions.Like(i.ItemName, term));
        }

        // SDS §9.3.3 — cross-company visibility is default-deny; a scoped role only sees
        // items authorized for one of its own units. Unscoped roles see the catalogue.
        if (!_user.IsSystemAdmin && IsScopedOnlyRole())
        {
            var units = _user.BusinessUnitIds.ToList();
            query = query.Where(i => _db.BusinessUnitItems.Any(b =>
                b.ItemId == i.ItemId && b.IsAuthorized && units.Contains(b.BusinessUnitId)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(i => i.ItemCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemSearchResponse.ItemSummaryDto
            {
                ItemId = i.ItemId,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                ItemStatus = i.ItemStatus,
                CategoryCode = i.Category.CategoryCode,
                BaseUom = i.BaseUOM.UOMCode
            })
            .ToListAsync(ct);

        return new ItemSearchResponse
        {
            Items = rows,
            Pagination = new ItemSearchResponse.PaginationDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        };
    }

    // ----------------------------------------------------------------- patch
    public async Task<PatchItemResponse> PatchAsync(long itemId, PatchItemRequest request, CancellationToken ct)
    {
        var item = await LoadItemGraphAsync(itemId, ct);
        AssertWriteScope(item);

        if (item.ItemStatus is ItemStatuses.Obsolete)
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                "An Obsolete item cannot be edited; Obsolete is terminal (SDS 8.1).");

        if (item.ItemStatus is ItemStatuses.PendingApproval)
            throw ApiException.Conflict(ErrorCodes.InvalidStatusTransition,
                "The item is already awaiting approval; resolve the open request first.");

        if (request.ItemCode is { Length: > 0 } && request.ItemCode != item.ItemCode)
        {
            // SDS §4.10 / §9.1.3 — renaming is blocked once transactional history exists.
            if (await HasTransactionalHistoryAsync(itemId, ct))
                throw ApiException.Conflict(ErrorCodes.ItemCodeLocked,
                    "Item code cannot be changed after transactional history exists.", "itemCode");

            if (await _db.Items.AnyAsync(i => i.ItemCode == request.ItemCode && i.ItemId != itemId, ct))
                throw ApiException.Conflict(ErrorCodes.ItemCodeDuplicate,
                    $"Item code '{request.ItemCode}' already exists.", "itemCode");
        }

        // SDS §10.6 — an Active item's edit does not apply directly; it opens an approval.
        if (item.ItemStatus == ItemStatuses.Active)
            return await OpenReapprovalAsync(item, request, ct);

        ApplyScalarChanges(item, request);
        ValidateFlags(item.CanPurchase, item.CanSell, item.CanManufacture, item.CanStock);
        item.ModifiedBy = _user.UserId;
        item.ModifiedDate = DateTime.UtcNow;

        if (request.Attributes is { Count: > 0 })
            await _attributes.ApplyAttributesAsync(item, request.Attributes, _user.UserId, ct);

        await _db.SaveChangesAsync(ct);

        return new PatchItemResponse
        {
            ItemId = item.ItemId,
            ItemStatus = item.ItemStatus,
            Links = { ["self"] = $"/api/v1/items/{item.ItemId}" }
        };
    }

    private async Task<PatchItemResponse> OpenReapprovalAsync(ItemMaster item, PatchItemRequest request, CancellationToken ct)
    {
        var workflow = await ResolveWorkflowAsync(item, null, ct);

        var pendingVersion = item.VersionNumber + 1;
        var payload = JsonSerializer.Serialize(request);

        var approval = new ItemApprovalRequest
        {
            ItemId = item.ItemId,
            WorkflowTemplateId = workflow.WorkflowTemplateId,
            RequestedBy = _user.UserId,
            RequestedDate = DateTime.UtcNow,
            CurrentStepOrder = workflow.Steps.Where(s => s.IsActive).Min(s => s.StepOrder),
            OverallStatus = ApprovalStatuses.Pending,
            PendingVersionNumber = pendingVersion,
            PendingChangeJson = payload,
            ChangeReason = request.ChangeReason,
            CreatedBy = _user.UserId
        };
        _db.ItemApprovalRequests.Add(approval);

        item.ItemStatus = ItemStatuses.PendingApproval;
        item.ModifiedBy = _user.UserId;
        item.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new PatchItemResponse
        {
            ItemId = item.ItemId,
            ItemStatus = item.ItemStatus,
            PendingVersionNumber = pendingVersion,
            ApprovalRequestId = approval.RequestId,
            Links = { ["approvalRequest"] = $"/api/v1/approval-requests/{approval.RequestId}" }
        };
    }

    // -------------------------------------------------------------- versions
    public async Task<IReadOnlyList<ItemVersionDto>> GetVersionsAsync(long itemId, CancellationToken ct)
    {
        if (!await _db.Items.AnyAsync(i => i.ItemId == itemId, ct))
            throw ApiException.NotFound($"Item {itemId} was not found.");

        var versions = await _db.ItemVersions.AsNoTracking()
            .Where(v => v.ItemId == itemId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        return versions.Select(v => new ItemVersionDto
        {
            ItemVersionId = v.ItemVersionId,
            VersionNumber = v.VersionNumber,
            ChangeReason = v.ChangeReason,
            ApprovalRequestId = v.ApprovalRequestId,
            CreatedDate = v.CreatedDate,
            CreatedBy = v.CreatedBy,
            Snapshot = SafeParse(v.SnapshotJson)
        }).ToList();
    }

    // -------------------------------------------------- business unit mapping
    public async Task<ItemDetailResponse> AuthorizeBusinessUnitsAsync(long itemId, AuthorizeBusinessUnitsRequest request,
                                                                      CancellationToken ct)
    {
        var item = await LoadItemGraphAsync(itemId, ct);
        AssertWriteScope(item);

        if (!_user.IsInScope(request.BusinessUnitIds))
            throw ApiException.Forbidden(ErrorCodes.ItemNotAuthorizedForEntity,
                "One or more requested business units are outside your scope.");

        foreach (var unitId in request.BusinessUnitIds.Distinct())
        {
            if (!await _db.BusinessUnits.AnyAsync(u => u.BusinessUnitId == unitId && u.IsActive, ct))
                throw ApiException.Validation($"Business unit {unitId} was not found or is inactive.", "businessUnitIds");

            var existing = item.BusinessUnitItems.FirstOrDefault(b => b.BusinessUnitId == unitId);
            if (existing is null)
            {
                _db.BusinessUnitItems.Add(new BusinessUnitItem
                {
                    ItemId = itemId,
                    BusinessUnitId = unitId,
                    IsAuthorized = request.IsAuthorized,
                    AuthorizedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    CreatedBy = _user.UserId
                });
            }
            else
            {
                // §5.3 — revoking sets IsAuthorized = 0; the row and its history stay.
                existing.IsAuthorized = request.IsAuthorized;
                existing.ModifiedBy = _user.UserId;
                existing.ModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(itemId, ct);
    }

    // --------------------------------------------------------------- helpers
    public async Task<string> BuildSnapshotJsonAsync(long itemId, CancellationToken ct)
    {
        var item = await LoadItemGraphAsync(itemId, ct);
        var snapshot = new
        {
            itemId = item.ItemId,
            itemCode = item.ItemCode,
            itemName = item.ItemName,
            description = item.Description,
            itemStatus = item.ItemStatus,
            versionNumber = item.VersionNumber,
            classification = new
            {
                categoryId = item.ItemCategoryId,
                groupId = item.ItemGroupId,
                subGroupId = item.ItemSubGroupId,
                familyId = item.ItemFamilyId
            },
            flags = new
            {
                item.CanPurchase, item.CanSell, item.CanManufacture,
                item.CanStock, item.CanTransfer, item.IsSerialControlled, item.IsLotControlled
            },
            baseUomId = item.BaseUOMId,
            attributeTemplateId = item.AttributeTemplateId,
            attributes = item.Attributes.Select(a => new
            {
                attributeCode = a.Definition.AttributeCode,
                value = ToScalar(a)
            }),
            businessUnitIds = item.BusinessUnitItems.Where(b => b.IsAuthorized).Select(b => b.BusinessUnitId)
        };
        return JsonSerializer.Serialize(snapshot);
    }

    internal async Task<ItemMaster> LoadItemGraphAsync(long itemId, CancellationToken ct) =>
        await _db.Items
            .Include(i => i.Category)
            .Include(i => i.Group)
            .Include(i => i.SubGroup)
            .Include(i => i.Family)
            .Include(i => i.BaseUOM)
            .Include(i => i.Attributes).ThenInclude(a => a.Definition)
            .Include(i => i.BusinessUnitItems).ThenInclude(b => b.BusinessUnit)
            .Include(i => i.Packaging).ThenInclude(p => p.PackagingUOM)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
        ?? throw ApiException.NotFound($"Item {itemId} was not found.");

    internal async Task<ApprovalWorkflowTemplate> ResolveWorkflowAsync(ItemMaster item, string? templateCode, CancellationToken ct)
    {
        var query = _db.ApprovalWorkflowTemplates.Include(w => w.Steps).AsQueryable();

        ApprovalWorkflowTemplate? workflow = null;

        if (!string.IsNullOrWhiteSpace(templateCode))
        {
            workflow = await query.FirstOrDefaultAsync(w => w.TemplateCode == templateCode && w.IsActive, ct)
                ?? throw ApiException.Validation($"Approval workflow '{templateCode}' was not found.", "workflowTemplateCode");
        }

        // SDS §8.2 — the workflow defaults to the item's category, else the fallback template.
        workflow ??= await query.FirstOrDefaultAsync(w => w.ItemCategoryId == item.ItemCategoryId && w.IsActive, ct);
        workflow ??= await query.FirstOrDefaultAsync(w => w.TemplateCode == "STANDARD-ITEM" && w.IsActive, ct);

        if (workflow is null || workflow.Steps.Count(s => s.IsActive) == 0)
            throw ApiException.Unprocessable(ErrorCodes.ValidationFailed,
                "No approval workflow with at least one step is configured for this item.");

        return workflow;
    }

    private bool IsScopedOnlyRole() =>
        _user.Roles.Count > 0 &&
        _user.Roles.All(r => r is RoleNames.WarehouseClerk or RoleNames.SalesUser or RoleNames.CategoryManager);

    private void AssertReadScope(ItemMaster item)
    {
        if (_user.IsSystemAdmin || !IsScopedOnlyRole()) return;

        var units = item.BusinessUnitItems.Where(b => b.IsAuthorized).Select(b => b.BusinessUnitId).ToList();
        if (units.Count > 0 && !_user.IsInScope(units))
            throw ApiException.Forbidden(ErrorCodes.ItemNotAuthorizedForEntity,
                $"Item {item.ItemId} is not authorized for any business unit in your scope.");
    }

    private void AssertWriteScope(ItemMaster item)
    {
        AssertReadScope(item);
        if (!_user.IsSystemAdmin && !_user.HasRole(RoleNames.CategoryManager))
            throw ApiException.Forbidden(ErrorCodes.Forbidden,
                "Only a Category Manager may create or edit items (SDS 12.3).");
    }

    private async Task<bool> HasTransactionalHistoryAsync(long itemId, CancellationToken ct) =>
        await _db.WarehouseItems.AnyAsync(w => w.ItemId == itemId && (w.QuantityOnHand != 0 || w.QuantityReserved != 0), ct)
        || await _db.ItemVersions.AnyAsync(v => v.ItemId == itemId, ct);

    private static void ApplyScalarChanges(ItemMaster item, PatchItemRequest request)
    {
        if (request.ItemCode is { Length: > 0 }) item.ItemCode = request.ItemCode;
        if (request.ItemName is { Length: > 0 }) item.ItemName = request.ItemName;
        if (request.Description is not null) item.Description = request.Description;
        if (request.CanPurchase.HasValue) item.CanPurchase = request.CanPurchase.Value;
        if (request.CanSell.HasValue) item.CanSell = request.CanSell.Value;
        if (request.CanManufacture.HasValue) item.CanManufacture = request.CanManufacture.Value;
        if (request.CanStock.HasValue) item.CanStock = request.CanStock.Value;
        if (request.CanTransfer.HasValue) item.CanTransfer = request.CanTransfer.Value;
        if (request.IsSerialControlled.HasValue) item.IsSerialControlled = request.IsSerialControlled.Value;
        if (request.IsLotControlled.HasValue) item.IsLotControlled = request.IsLotControlled.Value;
    }

    private static void ValidateFlags(bool canPurchase, bool canSell, bool canManufacture, bool canStock)
    {
        // SDS §12.1 rule 3.
        if (!canPurchase && !canSell && !canManufacture)
            throw ApiException.Validation(
                "At least one of canPurchase, canSell or canManufacture must be true.", "flags");

        // SDS §4.10 CK_ItemMaster_StockFlags — a manufactured output must be stockable.
        if (canManufacture && !canStock)
            throw ApiException.Validation("A manufacturable item must also be stockable.", "canStock");
    }

    internal static object? ToScalar(ItemAttribute? a)
    {
        if (a is null) return null;
        if (a.ValueNumber.HasValue) return a.ValueNumber.Value;
        if (a.ValueBoolean.HasValue) return a.ValueBoolean.Value;
        if (a.ValueDate.HasValue) return a.ValueDate.Value.ToString("yyyy-MM-dd");
        return a.ValueText;
    }

    private static JsonElement SafeParse(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return JsonDocument.Parse("{}").RootElement.Clone(); }
    }
}
