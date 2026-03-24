using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Outbox;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashFlow.IntegrationTests;

public sealed class OutboxDispatcherIntegrationTests
{
    [Fact]
    public async Task Dispatcher_ShouldPublishPendingOutboxMessage()
    {
        var publisher = new RecordingPublisher();
        await using var provider = BuildProvider(publisher);
        await SeedOutboxAsync(provider, "payload-1");

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var service = new OutboxDispatcherBackgroundService(scopeFactory, NullLogger<OutboxDispatcherBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();
        var outbox = await dbContext.OutboxMessages.SingleAsync();

        Assert.Single(publisher.PublishedMessages);
        Assert.NotNull(outbox.ProcessedAtUtc);
        Assert.Equal(0, outbox.Attempts);
    }

    [Fact]
    public async Task Dispatcher_WhenPublisherFails_ShouldKeepMessagePendingAndIncrementAttempts()
    {
        var publisher = new ThrowingPublisher();
        await using var provider = BuildProvider(publisher);
        await SeedOutboxAsync(provider, "payload-2");

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var service = new OutboxDispatcherBackgroundService(scopeFactory, NullLogger<OutboxDispatcherBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await Task.Delay(500, CancellationToken.None);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();
        var outbox = await dbContext.OutboxMessages.SingleAsync();

        Assert.Null(outbox.ProcessedAtUtc);
        Assert.True(outbox.Attempts >= 1);
        Assert.False(string.IsNullOrWhiteSpace(outbox.LastError));
    }

    private static async Task SeedOutboxAsync(ServiceProvider provider, string payload)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "TestEvent",
            RoutingKey = "cashflow.ledger.entry.registered",
            Payload = payload,
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 0
        });

        await dbContext.SaveChangesAsync();
    }

    private static ServiceProvider BuildProvider(IRabbitMqPublisher publisher)
    {
        var databaseName = $"cashflow-outbox-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<CashFlowDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IRabbitMqPublisher>(publisher);

        return services.BuildServiceProvider();
    }

    private sealed class RecordingPublisher : IRabbitMqPublisher
    {
        public List<(string RoutingKey, string Payload)> PublishedMessages { get; } = [];

        public Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken)
        {
            PublishedMessages.Add((routingKey, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IRabbitMqPublisher
    {
        public Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Falha simulada de publicação");
    }
}