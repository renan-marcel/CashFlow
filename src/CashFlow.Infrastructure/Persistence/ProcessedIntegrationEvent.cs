namespace CashFlow.Infrastructure.Persistence;

public sealed class ProcessedIntegrationEvent
{
    public Guid Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }
}