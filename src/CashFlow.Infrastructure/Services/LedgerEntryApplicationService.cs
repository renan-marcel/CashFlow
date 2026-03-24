using System.Text.Json;
using CashFlow.Application.Ledger;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using CashFlow.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Services;

public sealed class LedgerEntryApplicationService(
    CashFlowDbContext dbContext,
    IValidator<LedgerEntry> ledgerEntryValidator) : ILedgerEntryApplicationService
{
    public async Task<CreateLedgerEntryResult> CreateAsync(CreateLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        var existing = await dbContext.LedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.MerchantId == command.MerchantId && entry.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            return new CreateLedgerEntryResult(
                existing.Id,
                existing.MerchantId,
                existing.Type.ToString().ToLowerInvariant(),
                existing.Amount,
                existing.OccurredAtUtc,
                existing.Description,
                existing.IdempotencyKey,
                true);
        }

        var type = command.Type.Trim().ToLowerInvariant() switch
        {
            "credit" => LedgerEntryType.Credit,
            "debit" => LedgerEntryType.Debit,
            _ => throw new ArgumentException("type deve ser credit ou debit", nameof(command))
        };

        var occurredAtUtc = DateTime.SpecifyKind(command.OccurredAt, DateTimeKind.Utc);
        var ledgerEntry = new LedgerEntry(
            command.MerchantId,
            type,
            command.Amount,
            occurredAtUtc,
            command.Description,
            command.IdempotencyKey);

        // Validar a entidade LedgerEntry
        var validationResult = await ledgerEntryValidator.ValidateAsync(ledgerEntry, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Falha na validação do LedgerEntry: {errorMessages}", validationResult.Errors);
        }

        var integrationEvent = new LedgerEntryRegisteredIntegrationEvent(
            Guid.NewGuid(),
            ledgerEntry.Id,
            ledgerEntry.MerchantId,
            type.ToString().ToLowerInvariant(),
            ledgerEntry.Amount,
            ledgerEntry.OccurredAtUtc,
            DateOnly.FromDateTime(ledgerEntry.OccurredAtUtc));

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = nameof(LedgerEntryRegisteredIntegrationEvent),
            RoutingKey = "cashflow.ledger.entry.registered",
            Payload = JsonSerializer.Serialize(integrationEvent),
            OccurredAtUtc = DateTime.UtcNow,
            Attempts = 0
        };

        dbContext.LedgerEntries.Add(ledgerEntry);
        dbContext.OutboxMessages.Add(outbox);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateLedgerEntryResult(
            ledgerEntry.Id,
            ledgerEntry.MerchantId,
            integrationEvent.Type,
            ledgerEntry.Amount,
            ledgerEntry.OccurredAtUtc,
            ledgerEntry.Description,
            ledgerEntry.IdempotencyKey,
            false);
    }
}