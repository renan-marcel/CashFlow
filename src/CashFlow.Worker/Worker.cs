using CashFlow.Application.Ledger;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace CashFlow.Worker;

public class Worker(ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
   [FromKeyedServices("rabbitmq")] IConnection connection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Iniciando consumidor de consolidação...");
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqOptions.ExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: RabbitMqOptions.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: RabbitMqOptions.QueueName,
                exchange: RabbitMqOptions.ExchangeName,
                routingKey: RabbitMqOptions.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                try
                {
                    var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                    var integrationEvent = JsonSerializer.Deserialize<LedgerEntryRegisteredIntegrationEvent>(payload);

                    if (integrationEvent is null)
                    {
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                        return;
                    }

                    using var scope = scopeFactory.CreateScope();
                    var consolidationService = scope.ServiceProvider.GetRequiredService<LedgerConsolidationService>();
                    var success = await consolidationService.ApplyAsync(integrationEvent, stoppingToken);

                    if (success)
                    {
                        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Falha no consumo da fila de consolidação");
                    try
                    {
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                    }
                    catch (Exception nackException)
                    {
                        logger.LogError(nackException, "Erro ao fazer NACK da mensagem");
                    }
                }
            };

            await channel.BasicConsumeAsync(RabbitMqOptions.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            logger.LogInformation("Consumidor de consolidação ativo na fila {QueueName}", RabbitMqOptions.QueueName);

            // Aguardar até que o token de parada seja acionado
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Consumidor de consolidação foi parado");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro crítico no consumidor de consolidação");
            throw;
        }
    }
}
