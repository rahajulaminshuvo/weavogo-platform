using System.Security.Claims;
using WeavoGo.Master.Api.Common;

namespace WeavoGo.Master.Api.Services;

public interface ICurrentUser
{
    int UserId { get; }
    string UserName { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<int> BusinessUnitIds { get; }
    bool IsSystemAdmin { get; }
    bool HasRole(string roleName);

    /// <summary>
    /// SDS §12.3 — a scoped permission applies only within the caller's own business units.
    /// A System Administrator is unscoped by design (§12.3 closing note).
    /// </summary>
    bool IsInScope(IEnumerable<int> businessUnitIds);
}

public sealed class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return;

        UserId = int.TryParse(principal.FindFirst(ClaimTypesEx.UserId)?.Value, out var id) ? id : 0;
        UserName = principal.Identity.Name ?? string.Empty;
        Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        BusinessUnitIds = principal.FindAll(ClaimTypesEx.BusinessUnitIds)
                                   .Select(c => int.TryParse(c.Value, out var b) ? b : 0)
                                   .Where(b => b > 0).ToList();
    }

    public int UserId { get; }
    public string UserName { get; } = string.Empty;
    public IReadOnlyList<string> Roles { get; } = Array.Empty<string>();
    public IReadOnlyList<int> BusinessUnitIds { get; } = Array.Empty<int>();
    public bool IsSystemAdmin => HasRole(RoleNames.SystemAdministrator);

    public bool HasRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public bool IsInScope(IEnumerable<int> businessUnitIds)
    {
        if (IsSystemAdmin) return true;
        var units = businessUnitIds as IList<int> ?? businessUnitIds.ToList();
        if (units.Count == 0) return true;           // nothing to scope against yet (e.g. a new draft)
        return units.Any(BusinessUnitIds.Contains);
    }
}
