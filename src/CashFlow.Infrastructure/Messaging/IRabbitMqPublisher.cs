namespace CashFlow.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken);
}