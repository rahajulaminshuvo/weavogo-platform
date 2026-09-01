using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Services;

namespace WeavoGo.Master.Api.Controllers;

/// <summary>SDS §10.2 endpoint catalogue, base path /api/v1 (§10.1).</summary>
[ApiController]
[Route("api/v1/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemsController : ControllerBase
{
    private readonly IItemService _items;
    private readonly IApprovalService _approvals;

    public ItemsController(IItemService items, IApprovalService approvals)
    {
        _items = items;
        _approvals = approvals;
    }

    /// <summary>SDS §10.3 — create a new item in Draft status.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.CreateDraftItem)]
    [ProducesResponseType(typeof(CreateItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest request, CancellationToken ct)
    {
        var created = await _items.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { itemId = created.ItemId }, created);
    }

    /// <summary>SDS §10.4 — full item detail.</summary>
    [HttpGet("{itemId:long}")]
    [Authorize(Policy = Policies.ViewItem)]
    [ProducesResponseType(typeof(ItemDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemDetailResponse>> GetById(long itemId, CancellationToken ct)
        => Ok(await _items.GetAsync(itemId, ct));

    /// <summary>SDS §10.5 — filterable, paginated search. pageSize is capped at 100.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewItem)]
    [ProducesResponseType(typeof(ItemSearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ItemSearchResponse>> Search(
        [FromQuery] string? categoryCode,
        [FromQuery] string? status,
        [FromQuery] int? businessEntityId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _items.SearchAsync(categoryCode, status, businessEntityId, search, page, pageSize, ct));

    /// <summary>SDS §10.6 — partial update. Editing an Active item opens a re-approval.</summary>
    [HttpPatch("{itemId:long}")]
    [Authorize(Policy = Policies.EditDraftItem)]
    [ProducesResponseType(typeof(PatchItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatchItemResponse>> Patch(long itemId, [FromBody] PatchItemRequest request,
                                                             CancellationToken ct)
        => Ok(await _items.PatchAsync(itemId, request, ct));

    /// <summary>SDS §10.7 — submit a Draft item into its approval workflow.</summary>
    [HttpPost("{itemId:long}/submit")]
    [Authorize(Policy = Policies.SubmitForApproval)]
    [ProducesResponseType(typeof(ApprovalStateResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(long itemId, [FromBody] SubmitItemRequest? request, CancellationToken ct)
        => Accepted(await _approvals.SubmitAsync(itemId, request ?? new SubmitItemRequest(), ct));

    /// <summary>
    /// SDS §10.2 — record an approve/reject decision on the item's current step.
    /// The §10.7 form of this call lives on ApprovalRequestsController.
    /// </summary>
    [HttpPost("{itemId:long}/approval-actions")]
    [Authorize(Policy = Policies.ApproveStep)]
    [ProducesResponseType(typeof(ApprovalStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApprovalStateResponse>> ActOnItem(long itemId,
        [FromBody] ApprovalActionRequest request, CancellationToken ct)
        => Ok(await _approvals.RecordActionForItemAsync(itemId, request, ct));

    /// <summary>SDS §8.4 — version history.</summary>
    [HttpGet("{itemId:long}/versions")]
    [Authorize(Policy = Policies.ViewAuditAndVersions)]
    [ProducesResponseType(typeof(IReadOnlyList<ItemVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItemVersionDto>>> Versions(long itemId, CancellationToken ct)
        => Ok(await _items.GetVersionsAsync(itemId, ct));

    /// <summary>SDS §5.3 — authorize (or revoke) the item for business units.</summary>
    [HttpPost("{itemId:long}/business-units")]
    [Authorize(Policy = Policies.ManageBusinessUnitMapping)]
    [ProducesResponseType(typeof(ItemDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ItemDetailResponse>> AuthorizeBusinessUnits(long itemId,
        [FromBody] AuthorizeBusinessUnitsRequest request, CancellationToken ct)
        => Ok(await _items.AuthorizeBusinessUnitsAsync(itemId, request, ct));
}
