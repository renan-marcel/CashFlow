using CashFlow.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity type configuration for DailyBalance
/// Defines table structure, indexes, and property mappings
/// </summary>
public sealed class DailyBalanceConfiguration : IEntityTypeConfiguration<DailyBalance>
{
    public void Configure(EntityTypeBuilder<DailyBalance> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever(); // GUID v7 generated in domain

        builder.Property(e => e.MerchantId)
            .IsRequired();

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.Balance)
            .IsRequired()
            .HasPrecision(18, 2); // Support large balances

        builder.Property(e => e.UpdatedAtUtc)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(e => e.MerchantId)
            .HasDatabaseName("IX_DailyBalance_MerchantId");

        builder.HasIndex(e => e.Date)
            .HasDatabaseName("IX_DailyBalance_Date");

        builder.HasIndex(e => new { e.MerchantId, e.Date })
            .IsUnique()
            .HasDatabaseName("IX_DailyBalance_MerchantId_Date");

        builder.ToTable("daily_balances");
    }
}
