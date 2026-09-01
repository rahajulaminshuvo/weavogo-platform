using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Infrastructure;

namespace WeavoGo.Master.Api.Controllers;

/// <summary>
/// Read-only reference endpoints that back the cascading Category -> Group -> Sub-Group
/// -> Family dropdowns and the dynamic attribute form of the SDS §11.2.1 wireframe.
/// </summary>
[ApiController]
[Route("api/v1")]
//[Authorize(Policy = Policies.ViewItem)]
[Produces("application/json")]
public sealed class ClassificationController : ControllerBase
{
    private readonly ItemMasterDbContext _db;

    public ClassificationController(ItemMasterDbContext db) => _db = db;

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await _db.ItemCategories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new { c.ItemCategoryId, c.CategoryCode, c.CategoryName, c.Nature })
            .ToListAsync(ct));

    [HttpGet("categories/{categoryId:int}/groups")]
    public async Task<IActionResult> Groups(int categoryId, CancellationToken ct) =>
        Ok(await _db.ItemGroups.AsNoTracking().Where(g => g.ItemCategoryId == categoryId && g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .Select(g => new { g.ItemGroupId, g.GroupCode, g.GroupName })
            .ToListAsync(ct));

    [HttpGet("groups/{groupId:int}/sub-groups")]
    public async Task<IActionResult> SubGroups(int groupId, CancellationToken ct) =>
        Ok(await _db.ItemSubGroups.AsNoTracking().Where(s => s.ItemGroupId == groupId && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new { s.ItemSubGroupId, s.SubGroupCode, s.SubGroupName })
            .ToListAsync(ct));

    [HttpGet("sub-groups/{subGroupId:int}/families")]
    public async Task<IActionResult> Families(int subGroupId, CancellationToken ct) =>
        Ok(await _db.ItemFamilies.AsNoTracking().Where(f => f.ItemSubGroupId == subGroupId && f.IsActive)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new { f.ItemFamilyId, f.FamilyCode, f.FamilyName, f.DefaultAttributeTemplateId })
            .ToListAsync(ct));

    /// <summary>The attribute form definition the UI renders for a template (SDS §4.9).</summary>
    [HttpGet("attribute-templates/{templateId:int}")]
    public async Task<IActionResult> Template(int templateId, CancellationToken ct)
    {
        var template = await _db.AttributeTemplates.AsNoTracking()
            .Where(t => t.AttributeTemplateId == templateId)
            .Select(t => new { t.AttributeTemplateId, t.TemplateCode, t.TemplateName })
            .FirstOrDefaultAsync(ct);

        if (template is null) throw ApiException.NotFound($"Attribute template {templateId} was not found.");

        var attributes = await _db.TemplateAttributes.AsNoTracking()
            .Include(ta => ta.Definition)
            .Where(ta => ta.AttributeTemplateId == templateId && ta.IsActive)
            .OrderBy(ta => ta.DisplayOrder)
            .Select(ta => new
            {
                ta.Definition.AttributeCode,
                ta.Definition.AttributeName,
                ta.Definition.DataType,
                ta.Definition.UnitOfMeasure,
                ta.Definition.EnumOptions,
                ta.IsRequired,
                ta.DisplayOrder,
                ta.DefaultValue
            })
            .ToListAsync(ct);

        return Ok(new { template.AttributeTemplateId, template.TemplateCode, template.TemplateName, attributes });
    }

    [HttpGet("uoms")]
    public async Task<IActionResult> Uoms(CancellationToken ct) =>
        Ok(await _db.Uoms.AsNoTracking().Where(u => u.IsActive)
            .OrderBy(u => u.UOMCode)
            .Select(u => new { u.UOMId, u.UOMCode, u.UOMName, u.UOMType, u.DecimalPrecision })
            .ToListAsync(ct));

    [HttpGet("business-units")]
    public async Task<IActionResult> BusinessUnits(CancellationToken ct) =>
        Ok(await _db.BusinessUnits.AsNoTracking().Where(b => b.IsActive)
            .OrderBy(b => b.BusinessUnitId)
            .Select(b => new { b.BusinessUnitId, b.UnitCode, b.UnitName, b.BusinessType, b.CompanyId })
            .ToListAsync(ct));

    [HttpGet("workflow-templates")]
    public async Task<IActionResult> Workflows(CancellationToken ct) =>
        Ok(await _db.ApprovalWorkflowTemplates.AsNoTracking().Where(w => w.IsActive)
            .Select(w => new
            {
                w.WorkflowTemplateId,
                w.TemplateCode,
                w.TemplateName,
                w.ItemCategoryId,
                Steps = w.Steps.Where(s => s.IsActive).OrderBy(s => s.StepOrder)
                                .Select(s => new { s.StepOrder, s.ApproverRole, s.IsMandatory })
            })
            .ToListAsync(ct));

    /// <summary>SDS §8.3 — the field-level audit trail for an item.</summary>
    [HttpGet("items/{itemId:long}/audit-log")]
    [Authorize(Policy = Policies.ViewAuditAndVersions)]
    public async Task<IActionResult> AuditLog(long itemId, CancellationToken ct) =>
        Ok(await _db.ItemAuditLogs.AsNoTracking()
            .Where(a => a.ItemId == itemId)
            .OrderByDescending(a => a.ChangedDate).ThenByDescending(a => a.AuditLogId)
            .Select(a => new
            {
                a.AuditLogId, a.TableName, a.FieldName, a.OldValue, a.NewValue,
                a.ChangeType, a.ChangedBy, a.ChangedDate
            })
            .ToListAsync(ct));
}
