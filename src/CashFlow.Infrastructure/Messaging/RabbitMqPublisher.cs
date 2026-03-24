using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace CashFlow.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(
     [FromKeyedServices("rabbitmq")] IConnection connection,
    ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher
{
    private readonly IConnection _connection = connection;

    public async Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqOptions.ExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: RabbitMqOptions.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: RabbitMqOptions.QueueName,
                exchange: RabbitMqOptions.ExchangeName,
                routingKey: routingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);

            var body = Encoding.UTF8.GetBytes(payload).AsMemory();
            await channel.BasicPublishAsync(
                exchange: RabbitMqOptions.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: new BasicProperties { Persistent = true },
                body: body,
                cancellationToken: cancellationToken);

            logger.LogDebug("Mensagem publicada com sucesso no RabbitMQ. RoutingKey: {RoutingKey}", routingKey);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Publicação de mensagem foi cancelada. RoutingKey: {RoutingKey}", routingKey);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro ao publicar mensagem no RabbitMQ. RoutingKey: {RoutingKey}", routingKey);
            throw;
        }
    }
}