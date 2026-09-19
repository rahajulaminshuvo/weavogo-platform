// TODO(Security): promote this whole file to Weavo.BuildingBlocks.Security once
// that project exists. Every service reads these claim names, so they belong in
// the shared kernel rather than in Identity - but duplicating them into a
// BuildingBlock that does not yet exist would be worse.
namespace PlatformServices.Identity.Application.Common.Security;

/// <summary>
/// Claim names every WeavoGo service reads off the bearer token (B.7.1).
/// </summary>
/// <remarks>
/// Row-level tenancy (B.6) is enforced by each service filtering on
/// <see cref="TenantId"/>, <see cref="CompanyId"/> and
/// <see cref="BusinessUnitId"/> taken from here, so these strings are a
/// platform-wide contract: renaming one breaks every downstream filter.
/// </remarks>
public static class CustomClaimTypes
{
    /// <summary>Surrogate key of the authenticated UserAccount.</summary>
    public const string UserAccountId = "user_account_id";

    /// <summary>Outermost row-level security boundary (B.3.5).</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Company within the tenant (Section 5.2.1).</summary>
    public const string CompanyId = "company_id";

    /// <summary>Business unit within the company (Section 5.2.2).</summary>
    public const string BusinessUnitId = "business_unit_id";

    /// <summary>Role code granted in the requested scope.</summary>
    public const string Role = "role";

    /// <summary>Login and notification address.</summary>
    public const string Email = "email";
}
