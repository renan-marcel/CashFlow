using CashFlow.Application.Ledger;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using CashFlow.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Services;

public sealed class LedgerConsolidationService(
    CashFlowDbContext dbContext,
    IValidator<DailyBalance> dailyBalanceValidator)
{
    public async Task<bool> ApplyAsync(LedgerEntryRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var alreadyProcessed = await dbContext.ProcessedIntegrationEvents
            .AnyAsync(entry => entry.EventId == integrationEvent.EventId.ToString(), cancellationToken);

        if (alreadyProcessed)
        {
            return true;
        }

        var dailyBalance = await dbContext.DailyBalances
            .FirstOrDefaultAsync(
                entry => entry.MerchantId == integrationEvent.MerchantId && entry.Date == integrationEvent.BalanceDate,
                cancellationToken);

        if (dailyBalance is null)
        {
            dailyBalance = new DailyBalance(integrationEvent.MerchantId, integrationEvent.BalanceDate);
            dbContext.DailyBalances.Add(dailyBalance);
        }

        if (integrationEvent.Type.Equals("credit", StringComparison.OrdinalIgnoreCase))
        {
            dailyBalance.ApplyCredit(integrationEvent.Amount);
        }
        else
        {
            dailyBalance.ApplyDebit(integrationEvent.Amount);
        }

        // Validar a entidade DailyBalance após as modificações
        var validationResult = await dailyBalanceValidator.ValidateAsync(dailyBalance, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Falha na validação do DailyBalance: {errorMessages}", validationResult.Errors);
        }

        dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            Id = Guid.NewGuid(),
            EventId = integrationEvent.EventId.ToString(),
            ProcessedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}