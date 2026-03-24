namespace CashFlow.Application.Ledger;

public sealed record LedgerEntryRegisteredIntegrationEvent(
    Guid EventId,
    Guid LedgerEntryId,
    Guid MerchantId,
    string Type,
    decimal Amount,
    DateTime OccurredAtUtc,
    DateOnly BalanceDate);