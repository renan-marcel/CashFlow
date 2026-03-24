using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CashFlow.Infrastructure.Outbox;

public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

                var pendingMessages = await dbContext.OutboxMessages
                    .Where(message => message.ProcessedAtUtc == null)
                    .OrderBy(message => message.OccurredAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        await publisher.PublishAsync(message.RoutingKey, message.Payload, stoppingToken);
                        message.ProcessedAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception exception)
                    {
                        message.Attempts += 1;
                        message.LastError = exception.Message;
                        logger.LogError(exception, "Falha ao publicar mensagem da outbox {OutboxMessageId}", message.Id);
                    }
                }

                if (pendingMessages.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Erro durante processamento da outbox");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}