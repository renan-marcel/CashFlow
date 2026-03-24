namespace CashFlow.Application.Ledger;

public interface IDailyBalanceQueryService
{
    Task<DailyBalanceDto?> GetAsync(GetDailyBalanceQuery query, CancellationToken cancellationToken);
}