using CashFlow.Application.Ledger;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using CashFlow.Infrastructure.Persistence;
using CashFlow.Infrastructure.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.IntegrationTests;

public sealed class LedgerEntryApplicationServiceIntegrationTests
{
    [Fact]
    public async Task CreateAsync_ShouldPersistLedgerEntryAndOutbox()
    {
        await using var dbContext = CreateDbContext();
        var validator = new LedgerEntryValidator();
        var service = new LedgerEntryApplicationService(dbContext, validator);
        var merchantId = Guid.NewGuid();

        var command = new CreateLedgerEntryCommand(
            merchantId,
            "credit",
            120m,
            DateTime.UtcNow,
            "venda",
            "idem-001");

        var result = await service.CreateAsync(command, CancellationToken.None);

        var ledgerEntriesCount = await dbContext.LedgerEntries.CountAsync();
        var outboxCount = await dbContext.OutboxMessages.CountAsync();

        Assert.False(result.IsDuplicate);
        Assert.Equal(1, ledgerEntriesCount);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task CreateAsync_WithSameIdempotencyKey_ShouldNotDuplicate()
    {
        await using var dbContext = CreateDbContext();
        var validator = new LedgerEntryValidator();
        var service = new LedgerEntryApplicationService(dbContext, validator);
        var merchantId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var first = await service.CreateAsync(
            new CreateLedgerEntryCommand(merchantId, "credit", 80m, occurredAt, null, "idem-002"),
            CancellationToken.None);

        var second = await service.CreateAsync(
            new CreateLedgerEntryCommand(merchantId, "credit", 80m, occurredAt, null, "idem-002"),
            CancellationToken.None);

        var ledgerEntriesCount = await dbContext.LedgerEntries.CountAsync();
        var outboxCount = await dbContext.OutboxMessages.CountAsync();

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.LedgerEntryId, second.LedgerEntryId);
        Assert.Equal(1, ledgerEntriesCount);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task CreateAsync_WhenWorkerIsDown_ShouldStillPersistEntry()
    {
        await using var dbContext = CreateDbContext();
        var validator = new LedgerEntryValidator();
        var service = new LedgerEntryApplicationService(dbContext, validator);

        var result = await service.CreateAsync(
            new CreateLedgerEntryCommand(Guid.NewGuid(), "debit", 40m, DateTime.UtcNow, null, "idem-003"),
            CancellationToken.None);

        var entry = await dbContext.LedgerEntries.SingleAsync();
        var outbox = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(result.LedgerEntryId, entry.Id);
        Assert.Null(outbox.ProcessedAtUtc);
        Assert.False(result.IsDuplicate);
    }

    private static CashFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseInMemoryDatabase($"cashflow-integration-{Guid.NewGuid()}")
            .Options;

        return new CashFlowDbContext(options);
    }
}
