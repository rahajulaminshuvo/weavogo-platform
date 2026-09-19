namespace PlatformServices.Identity.Domain;

using Weavo.BuildingBlocks.Kernel;

/// <summary>
/// Grants a <see cref="Role"/> to a <see cref="UserAccount"/>, scoped to one
/// business unit (Appendix A.12.3).
/// </summary>
/// <remarks>
/// The scope is what makes this a junction rather than a flag: a user can hold
/// Category Manager in one unit and nothing in another. Unique on
/// (UserAccountId, RoleId, BusinessUnitId) per UQ_UserRole_UserRoleUnit.
/// </remarks>
public sealed class UserRole : Entity<int>
{
    /// <summary>The user granted the role.</summary>
    public int UserAccountId { get; private set; }

    /// <summary>The role granted.</summary>
    public int RoleId { get; private set; }

    /// <summary>The business unit this grant is scoped to.</summary>
    public int BusinessUnitId { get; private set; }

    /// <summary>Navigation to the granted role, loaded when resolving claims.</summary>
    public Role? Role { get; private set; }

    private UserRole()
    {
    }

    /// <summary>Grants a role to a user within one business unit.</summary>
    /// <param name="userAccountId">The user.</param>
    /// <param name="roleId">The role.</param>
    /// <param name="businessUnitId">The scope.</param>
    /// <returns>The new grant.</returns>
    public static UserRole Create(int userAccountId, int roleId, int businessUnitId) => new()
    {
        UserAccountId = userAccountId,
        RoleId = roleId,
        BusinessUnitId = businessUnitId,
    };
}
