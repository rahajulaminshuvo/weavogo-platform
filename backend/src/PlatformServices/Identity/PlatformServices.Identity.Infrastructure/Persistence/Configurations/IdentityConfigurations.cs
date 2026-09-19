namespace PlatformServices.Identity.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformServices.Identity.Domain;

/// <summary>Maps <see cref="UserAccount"/> to <c>identity.UserAccount</c> (A.12.2).</summary>
public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserAccount");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserAccountId").ValueGeneratedOnAdd();

        builder.Property(x => x.Username)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(x => x.LinkedPersonId);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Optimistic concurrency (B.4.4).
        builder.Property(x => x.RowVersion).IsRowVersion();

        // UQ_UserAccount_Email from A.12.2. Filtered so a soft-deleted row does
        // not permanently reserve an address (B.4.1).
        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("UQ_UserAccount_Email");

        builder.HasIndex(x => x.Username)
            .IsUnique()
            .HasDatabaseName("UQ_UserAccount_Username");

        builder.Ignore(x => x.DomainEvents);
    }
}

/// <summary>Maps <see cref="UserCredential"/> to <c>identity.UserCredential</c>.</summary>
public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserCredential", t => t.HasCheckConstraint(
            "CK_UserCredential_Type",
            "[CredentialType] IN ('Password', 'ExternalOIDC')"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserCredentialId").ValueGeneratedOnAdd();

        builder.Property(x => x.UserAccountId).IsRequired();

        builder.Property(x => x.CredentialType)
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue("Password");

        // Null when federated: Phase 2 stores no hash at all.
        builder.Property(x => x.PasswordHash).HasColumnType("varchar(500)");

        builder.Property(x => x.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.LockoutEndDate).HasColumnType("datetime2(3)");
        builder.Property(x => x.LastPasswordChangeDate).HasColumnType("datetime2(3)");
        builder.Property(x => x.LastLoginDate).HasColumnType("datetime2(3)");

        builder.Property(x => x.RowVersion).IsRowVersion();

        // One credential per account in Phase 1.
        builder.HasIndex(x => x.UserAccountId)
            .IsUnique()
            .HasDatabaseName("UQ_UserCredential_UserAccount");

        builder.HasOne<UserAccount>()
            .WithOne()
            .HasForeignKey<UserCredential>(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

/// <summary>Maps <see cref="Role"/> to <c>identity.Role</c> (A.12.1).</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Role");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("RoleId").ValueGeneratedOnAdd();

        builder.Property(x => x.RoleCode).HasColumnType("varchar(30)").IsRequired();
        builder.Property(x => x.RoleName).HasColumnType("varchar(100)").IsRequired();

        builder.HasIndex(x => x.RoleCode).IsUnique().HasDatabaseName("UQ_Role_Code");
    }
}

/// <summary>Maps <see cref="UserRole"/> to <c>identity.UserRole</c> (A.12.3).</summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserRole");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("UserRoleId").ValueGeneratedOnAdd();

        builder.Property(x => x.UserAccountId).IsRequired();
        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.BusinessUnitId).IsRequired();

        // UQ_UserRole_UserRoleUnit from A.12.3.
        builder.HasIndex(x => new { x.UserAccountId, x.RoleId, x.BusinessUnitId })
            .IsUnique()
            .HasDatabaseName("UQ_UserRole_UserRoleUnit");

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
