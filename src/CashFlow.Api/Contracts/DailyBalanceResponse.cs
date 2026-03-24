namespace CashFlow.API.Contracts;

public sealed record DailyBalanceResponse(Guid MerchantId, DateOnly Date, decimal Balance, DateTime UpdatedAtUtc);