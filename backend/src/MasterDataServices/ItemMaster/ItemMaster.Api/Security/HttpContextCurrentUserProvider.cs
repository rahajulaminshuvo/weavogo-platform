namespace ItemMaster.Api.Security;

using System.Security.Claims;
using ItemMaster.Application.Abstractions;

/// <summary>
/// Resolves the acting user from the validated JWT on the current request.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the ambient request.</param>
public sealed class HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserProvider
{
    /// <inheritdoc />
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when no authenticated user carrying a usable id claim is present.
    /// </exception>
    /// <remarks>
    /// Throws rather than falling back to a default id. A silent fallback would
    /// attribute every unauthenticated write to one synthetic account, which
    /// destroys the audit trail precisely when it matters most -- and it would
    /// mask an authentication misconfiguration instead of surfacing it.
    /// </remarks>
    public int GetCurrentUserId()
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No active request context.");

        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("uid")?.Value;

        if (!int.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException(
                "The authenticated principal carries no usable user id claim.");
        }

        return userId;
    }
}
