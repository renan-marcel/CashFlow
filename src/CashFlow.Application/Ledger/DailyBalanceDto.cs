namespace CashFlow.Application.Ledger;

public sealed record DailyBalanceDto(Guid MerchantId, DateOnly Date, decimal Balance, DateTime UpdatedAtUtc);