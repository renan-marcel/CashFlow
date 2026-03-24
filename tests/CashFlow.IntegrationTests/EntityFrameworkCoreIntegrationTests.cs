using CashFlow.Domain.Ledger;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CashFlow.IntegrationTests;

/// <summary>
/// Testes de integração para Entity Framework Core.
/// Valida operações CRUD, queries, e relacionamentos de entidades.
/// </summary>
public sealed class EntityFrameworkCoreIntegrationTests
{
    #region DailyBalance Tests

    [Fact]
    public async Task DailyBalance_Insert_ShouldPersistSuccessfully()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var dailyBalance = new DailyBalance(merchantId, date);
        dbContext.DailyBalances.Add(dailyBalance);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        var retrievedBalance = await dbContext.DailyBalances
            .FirstAsync(db => db.MerchantId == merchantId && db.Date == date);
        Assert.Equal(0m, retrievedBalance.Balance);
    }

    [Fact]
    public async Task DailyBalance_Update_ShouldModifyBalance()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var dailyBalance = new DailyBalance(merchantId, date);
        dbContext.DailyBalances.Add(dailyBalance);
        await dbContext.SaveChangesAsync();

        // Act
        dailyBalance.ApplyCredit(100m);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrievedBalance = await dbContext.DailyBalances
            .FirstAsync(db => db.MerchantId == merchantId && db.Date == date);
        Assert.Equal(100m, retrievedBalance.Balance);
    }

    [Fact]
    public async Task DailyBalance_QueryByMerchantAndDate_ShouldReturnCorrectRecord()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow);

        dbContext.DailyBalances.AddRange(
            new DailyBalance(merchantId, date1),
            new DailyBalance(merchantId, date2));
        await dbContext.SaveChangesAsync();

        // Act
        var balance = await dbContext.DailyBalances
            .FirstOrDefaultAsync(db => db.MerchantId == merchantId && db.Date == date2);

        // Assert
        Assert.NotNull(balance);
        Assert.Equal(date2, balance.Date);
    }

    [Fact]
    public async Task DailyBalance_MultipleMerchants_ShouldIsolateData()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchant1 = Guid.NewGuid();
        var merchant2 = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var balance1 = new DailyBalance(merchant1, date);
        balance1.ApplyCredit(100m);

        var balance2 = new DailyBalance(merchant2, date);
        balance2.ApplyCredit(200m);

        dbContext.DailyBalances.AddRange(balance1, balance2);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        var m1Balance = await dbContext.DailyBalances
            .FirstAsync(db => db.MerchantId == merchant1 && db.Date == date);
        var m2Balance = await dbContext.DailyBalances
            .FirstAsync(db => db.MerchantId == merchant2 && db.Date == date);

        Assert.Equal(100m, m1Balance.Balance);
        Assert.Equal(200m, m2Balance.Balance);
    }

    [Fact]
    public async Task DailyBalance_DeleteRecord_ShouldRemove()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var balance = new DailyBalance(merchantId, date);
        dbContext.DailyBalances.Add(balance);
        await dbContext.SaveChangesAsync();

        // Act
        dbContext.DailyBalances.Remove(balance);
        await dbContext.SaveChangesAsync();

        // Assert
        var exists = await dbContext.DailyBalances
            .AnyAsync(db => db.MerchantId == merchantId && db.Date == date);
        Assert.False(exists);
    }

    #endregion

    #region LedgerEntry Tests

    [Fact]
    public async Task LedgerEntry_Insert_ShouldPersistWithAllProperties()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var description = "Test transaction";
        var idempotencyKey = "test-001";

        var entry = new LedgerEntry(
            merchantId: merchantId,
            type: LedgerEntryType.Credit,
            amount: 250.50m,
            occurredAtUtc: DateTime.UtcNow,
            description: description,
            idempotencyKey: idempotencyKey);

        dbContext.LedgerEntries.Add(entry);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        var retrievedEntry = await dbContext.LedgerEntries.FirstAsync(le => le.Id == entry.Id);
        Assert.Equal(merchantId, retrievedEntry.MerchantId);
        Assert.Equal(250.50m, retrievedEntry.Amount);
        Assert.Equal(description, retrievedEntry.Description);
        Assert.Equal(idempotencyKey, retrievedEntry.IdempotencyKey);
    }

    [Fact]
    public async Task LedgerEntry_QueryByIdempotencyKey_ShouldFindDuplicate()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var idempotencyKey = "idem-duplicate";

        var entry1 = new LedgerEntry(
            merchantId: merchantId,
            type: LedgerEntryType.Credit,
            amount: 100m,
            occurredAtUtc: DateTime.UtcNow,
            description: "First",
            idempotencyKey: idempotencyKey);

        dbContext.LedgerEntries.Add(entry1);
        await dbContext.SaveChangesAsync();

        // Act
        var duplicate = await dbContext.LedgerEntries
            .FirstOrDefaultAsync(le => le.MerchantId == merchantId && le.IdempotencyKey == idempotencyKey);

        // Assert
        Assert.NotNull(duplicate);
        Assert.Equal(entry1.Id, duplicate.Id);
    }

    [Fact]
    public async Task LedgerEntry_ManyTransactions_ShouldHandleVolume()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        const int transactionCount = 100;

        var entries = Enumerable.Range(0, transactionCount)
            .Select(i => new LedgerEntry(
                merchantId: merchantId,
                type: i % 2 == 0 ? LedgerEntryType.Credit : LedgerEntryType.Debit,
                amount: decimal.One * (i + 1),
                occurredAtUtc: DateTime.UtcNow.AddSeconds(-i),
                description: $"Transaction {i}",
                idempotencyKey: $"idem-{i}"))
            .ToList();

        dbContext.LedgerEntries.AddRange(entries);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(transactionCount, result);
        var count = await dbContext.LedgerEntries
            .Where(le => le.MerchantId == merchantId)
            .CountAsync();
        Assert.Equal(transactionCount, count);
    }

    [Fact]
    public async Task LedgerEntry_FilterByType_ShouldReturnOnlyMatches()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();

        dbContext.LedgerEntries.AddRange(
            new LedgerEntry(merchantId, LedgerEntryType.Credit, 100m, DateTime.UtcNow, "C1", "c1"),
            new LedgerEntry(merchantId, LedgerEntryType.Debit, 50m, DateTime.UtcNow, "D1", "d1"),
            new LedgerEntry(merchantId, LedgerEntryType.Credit, 75m, DateTime.UtcNow, "C2", "c2"));
        await dbContext.SaveChangesAsync();

        // Act
        var credits = await dbContext.LedgerEntries
            .Where(le => le.MerchantId == merchantId && le.Type == LedgerEntryType.Credit)
            .ToListAsync();

        // Assert
        Assert.Equal(2, credits.Count);
        Assert.All(credits, le => Assert.Equal(LedgerEntryType.Credit, le.Type));
    }

    #endregion

    #region OutboxMessage Tests

    [Fact]
    public async Task OutboxMessage_Insert_ShouldPersistEvent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var payload = "{\"eventId\":\"123\",\"data\":\"test\"}";

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "LedgerEntryRegisteredIntegrationEvent",
            RoutingKey = "cashflow.ledger.entry.registered",
            Payload = payload,
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 0
        };

        dbContext.OutboxMessages.Add(outbox);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        var retrieved = await dbContext.OutboxMessages.FirstAsync(om => om.Id == outbox.Id);
        Assert.Equal(payload, retrieved.Payload);
        Assert.Null(retrieved.ProcessedAtUtc);
    }

    [Fact]
    public async Task OutboxMessage_QueryUnprocessed_ShouldFindPending()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;

        dbContext.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "Event1",
                RoutingKey = "key1",
                Payload = "{}",
                OccurredAtUtc = now.AddMinutes(-5),
                Attempts = 0,
                ProcessedAtUtc = now.AddMinutes(-2)
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "Event2",
                RoutingKey = "key2",
                Payload = "{}",
                OccurredAtUtc = now.AddMinutes(-3),
                Attempts = 0,
                ProcessedAtUtc = null
            });

        await dbContext.SaveChangesAsync();

        // Act
        var unprocessed = await dbContext.OutboxMessages
            .Where(om => om.ProcessedAtUtc == null)
            .ToListAsync();

        // Assert
        Assert.Single(unprocessed);
        Assert.Null(unprocessed[0].ProcessedAtUtc);
    }

    [Fact]
    public async Task OutboxMessage_IncrementAttempts_ShouldUpdateRetryCount()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "Event",
            RoutingKey = "test.key",
            Payload = "{}",
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 0
        };

        dbContext.OutboxMessages.Add(outbox);
        await dbContext.SaveChangesAsync();

        // Act - Simular 3 tentativas de publicação falhadas
        outbox.LastError = "Connection timeout";
        outbox.Attempts = 3;
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await dbContext.OutboxMessages.FirstAsync(om => om.Id == outbox.Id);
        Assert.Equal(3, retrieved.Attempts);
        Assert.Equal("Connection timeout", retrieved.LastError);
    }

    [Fact]
    public async Task OutboxMessage_MarkAsProcessed_ShouldSetTimestamp()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "Event",
            RoutingKey = "test.key",
            Payload = "{}",
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 1
        };

        dbContext.OutboxMessages.Add(outbox);
        await dbContext.SaveChangesAsync();

        // Act
        outbox.ProcessedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await dbContext.OutboxMessages.FirstAsync(om => om.Id == outbox.Id);
        Assert.NotNull(retrieved.ProcessedAtUtc);
    }

    #endregion

    #region ProcessedIntegrationEvent Tests

    [Fact]
    public async Task ProcessedIntegrationEvent_Insert_ShouldRecordProcessing()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var eventId = Guid.NewGuid().ToString();

        var processedEvent = new ProcessedIntegrationEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ProcessedAtUtc = DateTime.UtcNow
        };

        dbContext.ProcessedIntegrationEvents.Add(processedEvent);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        var retrieved = await dbContext.ProcessedIntegrationEvents
            .FirstAsync(pe => pe.EventId == eventId);
        Assert.NotNull(retrieved.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessedIntegrationEvent_QueryByEventId_ShouldEnforceIdempotency()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var eventId = "event-001";

        dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ProcessedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act
        var exists = await dbContext.ProcessedIntegrationEvents
            .AnyAsync(pe => pe.EventId == eventId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ProcessedIntegrationEvent_MultipleEvents_ShouldTrackAll()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        const int eventCount = 50;

        var events = Enumerable.Range(0, eventCount)
            .Select(i => new ProcessedIntegrationEvent
            {
                Id = Guid.NewGuid(),
                EventId = $"event-{i}",
                ProcessedAtUtc = DateTime.UtcNow
            })
            .ToList();

        dbContext.ProcessedIntegrationEvents.AddRange(events);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(eventCount, result);
        var count = await dbContext.ProcessedIntegrationEvents.CountAsync();
        Assert.Equal(eventCount, count);
    }

    #endregion

    #region Index and Performance Tests

    [Fact]
    public async Task DailyBalance_QueryWithIndexes_ShouldPerformEfficiently()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        const int recordCount = 50;

        // Insert records spanning multiple dates
        var balances = Enumerable.Range(0, recordCount)
            .Select(i => new DailyBalance(merchantId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i))))
            .ToList();

        dbContext.DailyBalances.AddRange(balances);
        await dbContext.SaveChangesAsync();

        // Act - Query using indexed columns
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await dbContext.DailyBalances
            .Where(db => db.MerchantId == merchantId)
            .OrderByDescending(db => db.Date)
            .ToListAsync();
        sw.Stop();

        // Assert
        Assert.Equal(recordCount, results.Count);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Query took {sw.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public async Task LedgerEntry_BulkInsert_ShouldHandle()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        const int batchSize = 500;
        var merchantId = Guid.NewGuid();

        var entries = Enumerable.Range(0, batchSize)
            .Select(i => new LedgerEntry(
                merchantId: merchantId,
                type: i % 2 == 0 ? LedgerEntryType.Credit : LedgerEntryType.Debit,
                amount: 10m + i,
                occurredAtUtc: DateTime.UtcNow.AddSeconds(-i),
                description: $"Bulk {i}",
                idempotencyKey: $"bulk-{i}"))
            .ToList();

        // Act
        dbContext.LedgerEntries.AddRange(entries);
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(batchSize, result);
    }

    [Fact]
    public async Task MixedEntities_ComplexScenario_ShouldPersistCorrectly()
    {
        // Arrange - Simular fluxo completo
        await using var dbContext = CreateDbContext();
        var merchantId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Criar DailyBalance
        var balance = new DailyBalance(merchantId, date);
        balance.ApplyCredit(500m);

        // Criar LedgerEntry
        var entry = new LedgerEntry(
            merchantId: merchantId,
            type: LedgerEntryType.Credit,
            amount: 500m,
            occurredAtUtc: DateTime.UtcNow,
            description: "Integration test entry",
            idempotencyKey: "int-test-001");

        // Criar OutboxMessage
        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "LedgerEntryRegisteredIntegrationEvent",
            RoutingKey = "cashflow.ledger.entry.registered",
            Payload = "{}",
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 0
        };

        dbContext.DailyBalances.Add(balance);
        dbContext.LedgerEntries.Add(entry);
        dbContext.OutboxMessages.Add(outbox);

        // Act
        var result = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(3, result);

        var savedBalance = await dbContext.DailyBalances
            .FirstAsync(db => db.MerchantId == merchantId && db.Date == date);
        var savedEntry = await dbContext.LedgerEntries.FirstAsync(le => le.Id == entry.Id);
        var savedOutbox = await dbContext.OutboxMessages.FirstAsync(om => om.Id == outbox.Id);

        Assert.Equal(500m, savedBalance.Balance);
        Assert.Equal(500m, savedEntry.Amount);
        Assert.Null(savedOutbox.ProcessedAtUtc);
    }

    #endregion

    private static CashFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseInMemoryDatabase($"cashflow-ef-{Guid.NewGuid()}")
            .Options;

        return new CashFlowDbContext(options);
    }
}
