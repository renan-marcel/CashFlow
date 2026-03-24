namespace CashFlow.Application.Ledger;

public sealed record GetDailyBalanceQuery(Guid MerchantId, DateOnly Date);