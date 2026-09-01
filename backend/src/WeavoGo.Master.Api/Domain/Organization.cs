namespace WeavoGo.Master.Api.Domain;

/// <summary>SDS §5.2.1 — the legal entity layer.</summary>
public class Company : AuditableEntity
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? TaxRegistrationNo { get; set; }
    public string Country { get; set; } = null!;

    public ICollection<BusinessUnit> BusinessUnits { get; set; } = new List<BusinessUnit>();
}

/// <summary>SDS §5.2.2 — the operating units that transact items.</summary>
public class BusinessUnit : AuditableEntity
{
    public int BusinessUnitId { get; set; }
    public int CompanyId { get; set; }
    public string UnitCode { get; set; } = null!;
    public string UnitName { get; set; } = null!;
    public string BusinessType { get; set; } = null!;

    public Company Company { get; set; } = null!;
}

public class UserAccount : AuditableEntity
{
    public int UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsSystemAdmin { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserBusinessUnit> UserBusinessUnits { get; set; } = new List<UserBusinessUnit>();
}

/// <summary>The roles of the SDS §12.3 security matrix, plus the §8.2.2 IT Security Review step role.</summary>
public class Role : AuditableEntity
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsScoped { get; set; } = true;
}

public class UserRole : AuditableEntity
{
    public int UserRoleId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public UserAccount User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

/// <summary>Defines "own business unit" for the SDS §12.3 scoped permissions.</summary>
public class UserBusinessUnit : AuditableEntity
{
    public int UserBusinessUnitId { get; set; }
    public int UserId { get; set; }
    public int BusinessUnitId { get; set; }
    public bool IsPrimary { get; set; }

    public UserAccount User { get; set; } = null!;
    public BusinessUnit BusinessUnit { get; set; } = null!;
}
