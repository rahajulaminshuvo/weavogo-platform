namespace ItemMaster.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ItemMaster.Application.Items.Commands;

/// <summary>HTTP surface for the item catalogue.</summary>
/// <param name="mediator">Dispatches commands and queries to their handlers.</param>
/// <remarks>
/// Actions stay thin: bind, send a MediatR message, map the outcome to a status
/// code. Business rules live in the domain; error translation is handled
/// centrally by <c>GlobalExceptionHandler</c>.
/// </remarks>
[ApiController]
[Route("api/v1/items")]
[Authorize]
public sealed class ItemMasterController(ISender mediator) : ControllerBase
{
    /// <summary>Gets an item record by its unique ID.</summary>
    /// <param name="id">Item identity.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The item, or 404 when it does not exist.</returns>
    [HttpGet("{id:long}", Name = nameof(GetById))]
    [Authorize(Policy = AuthorizationPolicies.CanReadItems)]
    [ProducesResponseType(typeof(ItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await mediator
            .Send(new GetItemByIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return Problem(
                title: "Not Found",
                detail: $"Item with ID {id} was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(result);
    }

    /// <summary>
    /// Creates a new ItemMaster record, emitting an ItemCreatedDomainEvent to
    /// the transactional Outbox.
    /// </summary>
    /// <param name="command">The item to create.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 with a Location header pointing at the new resource.</returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CanManageItems)]
    [ProducesResponseType(typeof(ItemCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return CreatedAtRoute(nameof(GetById), new { id = result.ItemId }, result);
    }

    /// <summary>Updates item status, staging an audit event.</summary>
    /// <param name="id">Item to transition.</param>
    /// <param name="request">The target status.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = AuthorizationPolicies.CanManageItems)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        long id,
        [FromBody] UpdateItemStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await mediator
            .Send(new UpdateItemStatusCommand(id, request.NewStatus), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}

/// <summary>Request body for a status transition.</summary>
/// <param name="NewStatus">Target lifecycle state.</param>
public sealed record UpdateItemStatusRequest(string NewStatus);

/// <summary>Authorization policy names, so controllers and start-up cannot drift.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Grants write access to the item catalogue.</summary>
    public const string CanManageItems = "CanManageItems";

    /// <summary>Grants read access to the item catalogue.</summary>
    public const string CanReadItems = "CanReadItems";
}
