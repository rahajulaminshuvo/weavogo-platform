using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Services;

namespace WeavoGo.Master.Api.Controllers;

/// <summary>SDS §10.7 — POST /api/v1/approval-requests/{requestId}/approval-actions.</summary>
[ApiController]
[Route("api/v1/approval-requests")]
[Authorize]
[Produces("application/json")]
public sealed class ApprovalRequestsController : ControllerBase
{
    private readonly IApprovalService _approvals;

    public ApprovalRequestsController(IApprovalService approvals) => _approvals = approvals;

    [HttpGet("{requestId:long}")]
    [Authorize(Policy = Policies.ViewItem)]
    [ProducesResponseType(typeof(ApprovalStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalStateResponse>> Get(long requestId, CancellationToken ct)
        => Ok(await _approvals.GetAsync(requestId, ct));

    /// <summary>Record the approve/reject decision on this request's current step.</summary>
    [HttpPost("{requestId:long}/approval-actions")]
    [Authorize(Policy = Policies.ApproveStep)]
    [ProducesResponseType(typeof(ApprovalStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalStateResponse>> Act(long requestId,
        [FromBody] ApprovalActionRequest request, CancellationToken ct)
        => Ok(await _approvals.RecordActionAsync(requestId, request, ct));
}
