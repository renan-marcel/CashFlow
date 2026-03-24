using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
            builder.Property(message => message.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
            builder.Property(message => message.Payload).HasColumnName("payload").IsRequired();
            builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
            builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc");
            builder.Property(message => message.Attempts).HasColumnName("attempts").IsRequired();
            builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2000);
            builder.HasIndex(message => message.ProcessedAtUtc);
        }
    }
}