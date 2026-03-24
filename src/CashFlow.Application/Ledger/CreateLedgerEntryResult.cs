namespace CashFlow.Application.Ledger;

public sealed record CreateLedgerEntryResult(
    Guid LedgerEntryId,
    Guid MerchantId,
    string Type,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description,
    string IdempotencyKey,
    bool IsDuplicate);