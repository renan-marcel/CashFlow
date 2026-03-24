using CashFlow.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity type configuration for LedgerEntry
/// Defines table structure, indexes, and property mappings
/// </summary>
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever(); // GUID v7 generated in domain

        builder.Property(e => e.MerchantId)
            .IsRequired();

        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<int>(); // Store enum as int

        builder.Property(e => e.Amount)
            .IsRequired()
            .HasPrecision(18, 2); // Support up to 999,999,999,999,999.99

        builder.Property(e => e.OccurredAtUtc)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        // Indexes for common queries
        builder.HasIndex(e => e.MerchantId)
            .HasDatabaseName("IX_LedgerEntry_MerchantId");

        builder.HasIndex(e => e.OccurredAtUtc)
            .HasDatabaseName("IX_LedgerEntry_OccurredAtUtc");

        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_LedgerEntry_IdempotencyKey");

        builder.HasIndex(e => new { e.MerchantId, e.OccurredAtUtc })
            .HasDatabaseName("IX_LedgerEntry_MerchantId_OccurredAtUtc");

        builder.ToTable("ledger_entries");
    }
}
