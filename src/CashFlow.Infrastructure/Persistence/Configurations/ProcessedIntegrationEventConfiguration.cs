using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations
{
    public class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
    {
        public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
        {
            builder.ToTable("processed_integration_events");
            builder.HasKey(processed => processed.Id);
            builder.Property(processed => processed.EventId).HasColumnName("event_id").IsRequired();
            builder.Property(processed => processed.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired();
            builder.HasIndex(processed => processed.EventId).IsUnique();
        }
    }
}