namespace ItemMaster.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ItemMaster.Infrastructure.Persistence.Outbox;

/// <summary>Maps <see cref="OutboxMessage"/> to <c>dbo.OutboxMessages</c>.</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages", "dbo");

        builder.HasKey(x => x.Id);

        // Set from the domain event's EventId, never database-generated.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.OccurredOnUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(x => x.ProcessedOnUtc)
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.Error)
            .HasColumnType("nvarchar(max)");

        // Filtered index: the dispatcher only ever scans unprocessed rows, so
        // the index stays small however large the archive grows. Ordered by
        // OccurredOnUtc to match the dispatcher's ORDER BY.
        builder.HasIndex(x => x.OccurredOnUtc)
            .HasFilter("[ProcessedOnUtc] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
