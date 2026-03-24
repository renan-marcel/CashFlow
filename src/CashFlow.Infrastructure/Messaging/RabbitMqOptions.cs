namespace CashFlow.Infrastructure.Messaging;

public static class RabbitMqOptions
{
    public static string ExchangeName { get; set; } = "cashflow.exchange";
    public static string QueueName { get; set; } = "cashflow.ledger.consolidation";
    public static string RoutingKey { get; set; } = "cashflow.ledger.entry.registered";
}