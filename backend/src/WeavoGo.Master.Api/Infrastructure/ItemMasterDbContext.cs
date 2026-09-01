using Microsoft.EntityFrameworkCore;
using WeavoGo.Master.Api.Domain;

namespace WeavoGo.Master.Api.Infrastructure;

/// <summary>
/// Database-first context. The SQL scripts under /database are the schema's source of
/// truth; this context maps onto them and never generates migrations of its own.
/// </summary>
public class ItemMasterDbContext : DbContext
{
    public ItemMasterDbContext(DbContextOptions<ItemMasterDbContext> options) : base(options) { }

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<ItemGroup> ItemGroups => Set<ItemGroup>();
    public DbSet<ItemSubGroup> ItemSubGroups => Set<ItemSubGroup>();
    public DbSet<ItemFamily> ItemFamilies => Set<ItemFamily>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<AttributeTemplate> AttributeTemplates => Set<AttributeTemplate>();
    public DbSet<TemplateAttribute> TemplateAttributes => Set<TemplateAttribute>();

    public DbSet<ItemMaster> Items => Set<ItemMaster>();
    public DbSet<ItemAttribute> ItemAttributes => Set<ItemAttribute>();
    public DbSet<BusinessUnitItem> BusinessUnitItems => Set<BusinessUnitItem>();
    public DbSet<ItemPackaging> ItemPackagings => Set<ItemPackaging>();
    public DbSet<Uom> Uoms => Set<Uom>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBusinessUnit> UserBusinessUnits => Set<UserBusinessUnit>();

    public DbSet<ApprovalWorkflowTemplate> ApprovalWorkflowTemplates => Set<ApprovalWorkflowTemplate>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<ItemApprovalRequest> ItemApprovalRequests => Set<ItemApprovalRequest>();
    public DbSet<ItemApprovalAction> ItemApprovalActions => Set<ItemApprovalAction>();
    public DbSet<ItemAuditLog> ItemAuditLogs => Set<ItemAuditLog>();
    public DbSet<ItemVersion> ItemVersions => Set<ItemVersion>();
    public DbSet<ItemObsolescence> ItemObsolescences => Set<ItemObsolescence>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseItem> WarehouseItems => Set<WarehouseItem>();
    public DbSet<QCParameterTemplate> QCParameterTemplates => Set<QCParameterTemplate>();
    public DbSet<QCParameter> QCParameters => Set<QCParameter>();
    public DbSet<ItemQCProfile> ItemQCProfiles => Set<ItemQCProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- classification -------------------------------------------------
        b.Entity<ItemCategory>(e =>
        {
            e.ToTable("ItemCategory");
            e.HasKey(x => x.ItemCategoryId);
            e.Property(x => x.CategoryCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.CategoryName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Nature).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
        });

        b.Entity<ItemGroup>(e =>
        {
            e.ToTable("ItemGroup");
            e.HasKey(x => x.ItemGroupId);
            e.Property(x => x.GroupCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.GroupName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
            e.HasOne(x => x.Category).WithMany(x => x.Groups).HasForeignKey(x => x.ItemCategoryId);
        });

        b.Entity<ItemSubGroup>(e =>
        {
            e.ToTable("ItemSubGroup");
            e.HasKey(x => x.ItemSubGroupId);
            e.Property(x => x.SubGroupCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.SubGroupName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
            e.HasOne(x => x.Group).WithMany(x => x.SubGroups).HasForeignKey(x => x.ItemGroupId);
        });

        b.Entity<ItemFamily>(e =>
        {
            e.ToTable("ItemFamily");
            e.HasKey(x => x.ItemFamilyId);
            e.Property(x => x.FamilyCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.FamilyName).HasMaxLength(100).IsUnicode(false);
            e.HasOne(x => x.SubGroup).WithMany(x => x.Families).HasForeignKey(x => x.ItemSubGroupId);
            e.HasOne(x => x.DefaultAttributeTemplate).WithMany().HasForeignKey(x => x.DefaultAttributeTemplateId);
        });

        b.Entity<AttributeDefinition>(e =>
        {
            e.ToTable("AttributeDefinition");
            e.HasKey(x => x.AttributeDefinitionId);
            e.Property(x => x.AttributeCode).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.AttributeName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.DataType).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsUnicode(false);
            e.HasIndex(x => x.AttributeCode).IsUnique();
        });

        b.Entity<AttributeTemplate>(e =>
        {
            e.ToTable("AttributeTemplate");
            e.HasKey(x => x.AttributeTemplateId);
            e.Property(x => x.TemplateCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.TemplateName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
        });

        b.Entity<TemplateAttribute>(e =>
        {
            e.ToTable("TemplateAttribute");
            e.HasKey(x => x.TemplateAttributeId);
            e.Property(x => x.DefaultValue).HasMaxLength(200);
            e.HasOne(x => x.Template).WithMany(x => x.TemplateAttributes).HasForeignKey(x => x.AttributeTemplateId);
            e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.AttributeDefinitionId);
        });

        // ---- item -----------------------------------------------------------
        b.Entity<Uom>(e =>
        {
            e.ToTable("UOM");
            e.HasKey(x => x.UOMId);
            e.Property(x => x.UOMCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.UOMName).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.UOMType).HasMaxLength(20).IsUnicode(false);
        });

        b.Entity<ItemMaster>(e =>
        {
            e.ToTable("ItemMaster");
            e.HasKey(x => x.ItemId);
            e.Property(x => x.ItemCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.ItemName).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ItemStatus).HasMaxLength(20).IsUnicode(false);
            e.HasIndex(x => x.ItemCode).IsUnique();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.ItemCategoryId);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.ItemGroupId);
            e.HasOne(x => x.SubGroup).WithMany().HasForeignKey(x => x.ItemSubGroupId);
            e.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.ItemFamilyId);
            e.HasOne(x => x.AttributeTemplate).WithMany().HasForeignKey(x => x.AttributeTemplateId);
            e.HasOne(x => x.BaseUOM).WithMany().HasForeignKey(x => x.BaseUOMId);
        });

        b.Entity<ItemAttribute>(e =>
        {
            e.ToTable("ItemAttribute");
            e.HasKey(x => x.ItemAttributeId);
            e.Property(x => x.ValueText).HasMaxLength(500);
            e.Property(x => x.ValueNumber).HasPrecision(18, 4);
            e.HasIndex(x => new { x.ItemId, x.AttributeDefinitionId }).IsUnique();
            e.HasOne(x => x.Item).WithMany(x => x.Attributes).HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.AttributeDefinitionId);
        });

        b.Entity<BusinessUnitItem>(e =>
        {
            e.ToTable("BusinessUnitItem");
            e.HasKey(x => x.BusinessUnitItemId);
            e.Property(x => x.LocalItemCode).HasMaxLength(30).IsUnicode(false);
            e.HasIndex(x => new { x.ItemId, x.BusinessUnitId }).IsUnique();
            e.HasOne(x => x.Item).WithMany(x => x.BusinessUnitItems).HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.BusinessUnit).WithMany().HasForeignKey(x => x.BusinessUnitId);
        });

        b.Entity<ItemPackaging>(e =>
        {
            e.ToTable("ItemPackaging");
            e.HasKey(x => x.ItemPackagingId);
            e.Property(x => x.QtyPerParentLevel).HasPrecision(18, 4);
            e.HasOne(x => x.Item).WithMany(x => x.Packaging).HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.PackagingUOM).WithMany().HasForeignKey(x => x.PackagingUOMId);
        });

        // ---- organization ---------------------------------------------------
        b.Entity<Company>(e =>
        {
            e.ToTable("Company");
            e.HasKey(x => x.CompanyId);
            e.Property(x => x.CompanyCode).HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.CompanyName).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.TaxRegistrationNo).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.Country).HasMaxLength(60).IsUnicode(false);
        });

        b.Entity<BusinessUnit>(e =>
        {
            e.ToTable("BusinessUnit");
            e.HasKey(x => x.BusinessUnitId);
            e.Property(x => x.UnitCode).HasMaxLength(15).IsUnicode(false);
            e.Property(x => x.UnitName).HasMaxLength(150).IsUnicode(false);
            e.Property(x => x.BusinessType).HasMaxLength(50).IsUnicode(false);
            e.HasOne(x => x.Company).WithMany(x => x.BusinessUnits).HasForeignKey(x => x.CompanyId);
        });

        b.Entity<UserAccount>(e =>
        {
            e.ToTable("UserAccount");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserName).HasMaxLength(60).IsUnicode(false);
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false);
            e.HasIndex(x => x.UserName).IsUnique();
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Role");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleCode).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.RoleName).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("UserRole");
            e.HasKey(x => x.UserRoleId);
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        b.Entity<UserBusinessUnit>(e =>
        {
            e.ToTable("UserBusinessUnit");
            e.HasKey(x => x.UserBusinessUnitId);
            e.HasOne(x => x.User).WithMany(x => x.UserBusinessUnits).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.BusinessUnit).WithMany().HasForeignKey(x => x.BusinessUnitId);
        });

        // ---- governance -----------------------------------------------------
        b.Entity<ApprovalWorkflowTemplate>(e =>
        {
            e.ToTable("ApprovalWorkflowTemplate");
            e.HasKey(x => x.WorkflowTemplateId);
            e.Property(x => x.TemplateCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.TemplateName).HasMaxLength(100).IsUnicode(false);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.ItemCategoryId);
        });

        b.Entity<ApprovalStep>(e =>
        {
            e.ToTable("ApprovalStep");
            e.HasKey(x => x.ApprovalStepId);
            e.Property(x => x.ApproverRole).HasMaxLength(50).IsUnicode(false);
            e.HasOne(x => x.Template).WithMany(x => x.Steps).HasForeignKey(x => x.WorkflowTemplateId);
        });

        b.Entity<ItemApprovalRequest>(e =>
        {
            e.ToTable("ItemApprovalRequest");
            e.HasKey(x => x.RequestId);
            e.Property(x => x.OverallStatus).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.ChangeReason).HasMaxLength(500).IsUnicode(false);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.WorkflowTemplateId);
        });

        b.Entity<ItemApprovalAction>(e =>
        {
            e.ToTable("ItemApprovalAction");
            e.HasKey(x => x.ActionId);
            e.Property(x => x.Decision).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Comments).HasMaxLength(1000).IsUnicode(false);
            e.HasOne(x => x.Request).WithMany(x => x.Actions).HasForeignKey(x => x.RequestId);
        });

        b.Entity<ItemAuditLog>(e =>
        {
            e.ToTable("ItemAuditLog");
            e.HasKey(x => x.AuditLogId);
            e.Property(x => x.TableName).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.FieldName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.OldValue).HasMaxLength(1000);
            e.Property(x => x.NewValue).HasMaxLength(1000);
            e.Property(x => x.ChangeType).HasMaxLength(20).IsUnicode(false);
        });

        b.Entity<ItemVersion>(e =>
        {
            e.ToTable("ItemVersion");
            e.HasKey(x => x.ItemVersionId);
            e.Property(x => x.ChangeReason).HasMaxLength(500).IsUnicode(false);
            e.HasIndex(x => new { x.ItemId, x.VersionNumber }).IsUnique();
        });

        b.Entity<ItemObsolescence>(e =>
        {
            e.ToTable("ItemObsolescence");
            e.HasKey(x => x.ObsolescenceId);
            e.Property(x => x.Reason).HasMaxLength(500).IsUnicode(false);
        });

        // ---- operations -----------------------------------------------------
        b.Entity<Warehouse>(e =>
        {
            e.ToTable("Warehouse");
            e.HasKey(x => x.WarehouseId);
            e.Property(x => x.WarehouseCode).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.WarehouseName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.WarehouseType).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Address).HasMaxLength(300).IsUnicode(false);
            e.HasOne(x => x.BusinessUnit).WithMany().HasForeignKey(x => x.BusinessUnitId);
        });

        b.Entity<WarehouseItem>(e =>
        {
            e.ToTable("WarehouseItem");
            e.HasKey(x => x.WarehouseItemId);
            e.Property(x => x.QuantityOnHand).HasPrecision(18, 4);
            e.Property(x => x.QuantityReserved).HasPrecision(18, 4);
            e.Property(x => x.BinLocation).HasMaxLength(30).IsUnicode(false);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        });

        b.Entity<QCParameterTemplate>(e =>
        {
            e.ToTable("QCParameterTemplate");
            e.HasKey(x => x.QCTemplateId);
            e.Property(x => x.TemplateCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.TemplateName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false);
        });

        b.Entity<QCParameter>(e =>
        {
            e.ToTable("QCParameter");
            e.HasKey(x => x.QCParameterId);
            e.Property(x => x.ParameterName).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.DataType).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.MinValue).HasPrecision(18, 4);
            e.Property(x => x.MaxValue).HasPrecision(18, 4);
            e.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.TestMethod).HasMaxLength(150).IsUnicode(false);
            e.HasOne(x => x.Template).WithMany(x => x.Parameters).HasForeignKey(x => x.QCTemplateId);
        });

        b.Entity<ItemQCProfile>(e =>
        {
            e.ToTable("ItemQCProfile");
            e.HasKey(x => x.ItemQCProfileId);
            e.Property(x => x.InspectionFrequency).HasMaxLength(20).IsUnicode(false);
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.QCTemplateId);
        });

        // ---- shared audit-column configuration (SDS §4.1) --------------------
        var auditable = b.Model.GetEntityTypes()
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        foreach (var entityType in auditable)
        {
            var entity = b.Entity(entityType.ClrType);
            entity.Property(nameof(AuditableEntity.CreatedDate)).HasColumnType("datetime2(3)")
                  .HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(nameof(AuditableEntity.ModifiedDate)).HasColumnType("datetime2(3)");
            entity.Property(nameof(AuditableEntity.RowVersion)).IsRowVersion();

            // The database never hard-deletes (Chapter 8); soft-deleted rows are hidden by default.
            entity.HasQueryFilter(BuildNotDeletedFilter(entityType.ClrType));
        }
    }

    private static System.Linq.Expressions.LambdaExpression BuildNotDeletedFilter(Type clrType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
        var body = System.Linq.Expressions.Expression.Equal(
            property, System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(body, parameter);
    }
}
