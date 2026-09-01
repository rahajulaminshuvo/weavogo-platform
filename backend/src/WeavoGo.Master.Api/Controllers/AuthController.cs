using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Services;

namespace WeavoGo.Master.Api.Controllers;

/// <summary>
/// Issues the bearer token that SDS §10.1 requires on every request. The mechanism is
/// outside the SDS's own scope; JWT with the Chapter 12 roles as claims is the choice here.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _user;

    public AuthController(ITokenService tokens, ICurrentUser user)
    {
        _tokens = tokens;
        _user = user;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _tokens.AuthenticateAsync(request, ct));

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        _user.UserId,
        _user.UserName,
        _user.Roles,
        _user.BusinessUnitIds,
        _user.IsSystemAdmin
    });
}
