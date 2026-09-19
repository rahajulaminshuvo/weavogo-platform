namespace PlatformServices.Identity.Domain;

using Weavo.BuildingBlocks.Kernel;

/// <summary>
/// A system permission grant (Appendix A.12.1).
/// </summary>
/// <remarks>
/// Distinct from Designation (A.2.3): Designation is an HR job title, Role is a
/// system permission. Seeded from A.12.1's sample data (CATEGORY_MGR,
/// FINANCE_CTRL, QC_MANAGER, IT_SECURITY, WAREHOUSE_CLERK, SALES_USER,
/// READ_ONLY, SYSTEM_ADMIN).
/// </remarks>
public sealed class Role : Entity<int>
{
    /// <summary>Unique machine code, e.g. <c>SYSTEM_ADMIN</c>.</summary>
    public string RoleCode { get; private set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string RoleName { get; private set; } = string.Empty;

    private Role()
    {
    }

    /// <summary>Creates a role.</summary>
    /// <param name="roleCode">Unique machine code.</param>
    /// <param name="roleName">Display name.</param>
    /// <returns>The new role.</returns>
    public static Role Create(string roleCode, string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        return new Role
        {
            RoleCode = roleCode.Trim().ToUpperInvariant(),
            RoleName = roleName.Trim(),
        };
    }
}
