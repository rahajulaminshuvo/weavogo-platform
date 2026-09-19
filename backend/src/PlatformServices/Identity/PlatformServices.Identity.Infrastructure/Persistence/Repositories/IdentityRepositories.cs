namespace PlatformServices.Identity.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using PlatformServices.Identity.Application.Contracts;
using PlatformServices.Identity.Application.DTOs;
using PlatformServices.Identity.Domain;

/// <summary>EF Core implementation of <see cref="IUserAccountRepository"/>.</summary>
/// <param name="dbContext">The unit of work for this request.</param>
public sealed class UserAccountRepository(IdentityDbContext dbContext) : IUserAccountRepository
{
    /// <inheritdoc />
    public async Task<UserAccount?> FindByUsernameAsync(
        string username, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var normalised = username.Trim();

        // Tracked: the caller may deactivate the account, and the concurrency
        // token must come along.
        return await dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.Username == normalised, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserAccount?> FindByIdAsync(
        int userAccountId, CancellationToken cancellationToken)
        => await dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.Id == userAccountId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        dbContext.UserAccounts.Add(account);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>EF Core implementation of <see cref="IUserCredentialRepository"/>.</summary>
/// <param name="dbContext">The unit of work for this request.</param>
public sealed class UserCredentialRepository(IdentityDbContext dbContext)
    : IUserCredentialRepository
{
    /// <inheritdoc />
    public async Task<UserCredential?> FindByUserAccountIdAsync(
        int userAccountId, CancellationToken cancellationToken)
        => await dbContext.UserCredentials
            .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(UserCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        dbContext.UserCredentials.Add(credential);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Resolves the tenancy a token is scoped to, from the user's role grants.
/// </summary>
/// <remarks>
/// <para>
/// A.12.3 scopes each grant to a <c>BusinessUnitId</c>, so tenancy follows the
/// grant rather than the user. This reads the grants, picks the requested unit
/// (or the only one, when unambiguous), and returns the roles held there.
/// </para>
/// <para>
/// <b>Known limitation.</b> Walking BusinessUnit up to Company and Tenant
/// requires <c>OrganizationSubsystem</c>, which is not built. Until it is,
/// <c>CompanyId</c> and <c>TenantId</c> come from configuration
/// (<c>Identity:DefaultCompanyId</c>, <c>Identity:DefaultTenantId</c>) so tokens
/// carry the claims downstream services filter on. Replace this with a real
/// lookup — or a gRPC call to OrganizationSubsystem — when that service lands.
/// </para>
/// </remarks>
/// <param name="dbContext">The unit of work for this request.</param>
/// <param name="options">Placeholder tenancy values.</param>
public sealed class UserContextResolver(
    IdentityDbContext dbContext,
    Microsoft.Extensions.Options.IOptions<OrganizationDefaults> options) : IUserContextResolver
{
    private readonly OrganizationDefaults _defaults = options.Value;

    /// <inheritdoc />
    public async Task<UserContext?> ResolveAsync(
        UserAccount userAccount,
        int? requestedBusinessUnitId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userAccount);

        var grants = await dbContext.UserRoles
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.UserAccountId == userAccount.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (grants.Count == 0)
        {
            return null;
        }

        int businessUnitId;

        if (requestedBusinessUnitId.HasValue)
        {
            if (!grants.Exists(g => g.BusinessUnitId == requestedBusinessUnitId.Value))
            {
                // The user holds no grant in the unit they asked for. Refusing
                // beats silently issuing a token for a different unit.
                return null;
            }

            businessUnitId = requestedBusinessUnitId.Value;
        }
        else
        {
            var distinctUnits = grants
                .Select(g => g.BusinessUnitId)
                .Distinct()
                .ToList();

            // Ambiguous: the caller must say which unit. Picking one would issue
            // a token with silently wrong row-level scope.
            if (distinctUnits.Count != 1)
            {
                return null;
            }

            businessUnitId = distinctUnits[0];
        }

        var roles = grants
            .Where(g => g.BusinessUnitId == businessUnitId && g.Role is not null)
            .Select(g => g.Role!.RoleCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new UserContext(
            UserAccountId: userAccount.Id,
            Username: userAccount.Username,
            Email: userAccount.Email,
            TenantId: _defaults.TenantId,
            CompanyId: _defaults.CompanyId,
            BusinessUnitId: businessUnitId,
            Roles: roles);
    }
}

/// <summary>
/// Placeholder tenancy values, bound from the <c>Identity</c> section.
/// </summary>
/// <remarks>
/// TODO(OrganizationSubsystem): delete this once TenantMaster, CompanyMaster and
/// BranchMaster exist and BusinessUnit can be walked up to its real Company and
/// Tenant. These values exist only so tokens carry non-null tenancy claims in
/// the interim.
/// </remarks>
public sealed class OrganizationDefaults
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Identity";

    /// <summary>Tenant stamped on every token until TenantMaster exists.</summary>
    public int TenantId { get; init; } = 1;

    /// <summary>Company stamped on every token until CompanyMaster exists.</summary>
    public int CompanyId { get; init; } = 1;
}
