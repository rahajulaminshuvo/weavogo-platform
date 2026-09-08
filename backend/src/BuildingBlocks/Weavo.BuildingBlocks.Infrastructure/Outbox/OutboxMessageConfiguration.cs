using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Weavo.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Maps <see cref="OutboxMessage"/> into the owning service's own database.
/// </summary>
/// <remarks>
/// Apply from each service's <c>OnModelCreating</c>. The schema is a
/// constructor parameter because services differ: ItemMaster uses <c>dbo</c>,
/// others may isolate their tables.
/// </remarks>
/// <param name="schema">Schema to create the table in.</param>
/// <param name="isSqlServer">
/// Whether the target is SQL Server. Controls the filtered-index dialect —
/// SQL Server quotes with brackets, PostgreSQL (WeavoMES, per B.4.8) with
/// double quotes. A filter written for one is invalid on the other.
/// </param>
public sealed class OutboxMessageConfiguration(string schema = "dbo", bool isSqlServer = true)
    : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages", schema);

        builder.HasKey(x => x.Id);

        // Set from the domain event's EventId, never database-generated.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Content).IsRequired();

        builder.Property(x => x.OccurredOnUtc).IsRequired();

        builder.Property(x => x.ProcessedOnUtc);

        builder.Property(x => x.Error);

        builder.Property(x => x.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Filtered (PostgreSQL: partial) index. The dispatcher scans only
        // unpublished rows, so the index stays small however large the archive
        // grows. Ordered by OccurredOnUtc to match the dispatcher's ORDER BY.
        var pendingFilter = isSqlServer
            ? "[ProcessedOnUtc] IS NULL"
            : "\"ProcessedOnUtc\" IS NULL";

        builder.HasIndex(x => x.OccurredOnUtc)
            .HasFilter(pendingFilter)
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
