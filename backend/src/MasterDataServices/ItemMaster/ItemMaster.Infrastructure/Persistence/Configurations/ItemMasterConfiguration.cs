namespace ItemMaster.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ItemMaster.Domain.Entities;

/// <summary>
/// Maps <see cref="ItemMasterRecord"/> to <c>dbo.ItemMaster</c>.
/// </summary>
/// <remarks>
/// Column types mirror <c>database/05_schema_item.sql</c> exactly, so an EF
/// migration generated from this model matches the hand-written DDL rather than
/// fighting it.
/// </remarks>
public sealed class ItemMasterConfiguration : IEntityTypeConfiguration<ItemMasterRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItemMasterRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ItemMaster", "dbo");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ItemId").ValueGeneratedOnAdd();

        builder.Property(x => x.ItemCode)
            .HasColumnName("ItemCode")
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.HasIndex(x => x.ItemCode).IsUnique().HasDatabaseName("UQ_ItemMaster_Code");

        builder.Property(x => x.ItemName)
            .HasColumnName("ItemName")
            .HasColumnType("nvarchar(200)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("Description")
            .HasColumnType("nvarchar(1000)");

        // Hierarchy ancestry. Denormalised on the row per the schema notes, so
        // a classification query needs no joins up the tree.
        builder.Property(x => x.ItemCategoryId).HasColumnName("ItemCategoryId").IsRequired();
        builder.Property(x => x.ItemGroupId).HasColumnName("ItemGroupId").IsRequired();
        builder.Property(x => x.ItemSubGroupId).HasColumnName("ItemSubGroupId").IsRequired();
        builder.Property(x => x.ItemFamilyId).HasColumnName("ItemFamilyId").IsRequired();
        builder.Property(x => x.AttributeTemplateId).HasColumnName("AttributeTemplateId");
        builder.Property(x => x.BaseUOMId).HasColumnName("BaseUOMId").IsRequired();

        // Operational flags.
        builder.Property(x => x.CanPurchase).HasColumnName("CanPurchase").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CanSell).HasColumnName("CanSell").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CanManufacture).HasColumnName("CanManufacture").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CanStock).HasColumnName("CanStock").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CanTransfer).HasColumnName("CanTransfer").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsSerialControlled).HasColumnName("IsSerialControlled").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsLotControlled).HasColumnName("IsLotControlled").IsRequired().HasDefaultValue(false);

        builder.Property(x => x.ItemStatus)
            .HasColumnName("ItemStatus")
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue("Draft");

        builder.Property(x => x.VersionNumber)
            .HasColumnName("VersionNumber")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.CreatedBy)
            .HasColumnName("CreatedBy")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(x => x.ModifiedBy)
            .HasColumnName("ModifiedBy");

        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate")
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.IsDeleted)
            .HasColumnName("IsDeleted")
            .IsRequired()
            .HasDefaultValue(false);

        // Optimistic concurrency: two clerks editing the same item concurrently
        // produce a DbUpdateConcurrencyException rather than a lost update.
        builder.Property(x => x.RowVersion)
            .HasColumnName("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken();

        // Domain events are dispatched through the Outbox, never persisted as
        // part of the aggregate itself.
        builder.Ignore(x => x.DomainEvents);
    }
}
