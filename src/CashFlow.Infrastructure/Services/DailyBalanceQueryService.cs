using CashFlow.Application.Ledger;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Services;

public sealed class DailyBalanceQueryService(CashFlowDbContext dbContext) : IDailyBalanceQueryService
{
    public async Task<DailyBalanceDto?> GetAsync(GetDailyBalanceQuery query, CancellationToken cancellationToken)
    {
        var balance = await dbContext.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.MerchantId == query.MerchantId && entry.Date == query.Date,
                cancellationToken);

        return balance is null
            ? null
            : new DailyBalanceDto(balance.MerchantId, balance.Date, balance.Balance, balance.UpdatedAtUtc);
    }
}