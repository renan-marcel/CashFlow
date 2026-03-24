namespace CashFlow.API.Contracts;

public sealed record LedgerEntryResponse(
    Guid LedgerEntryId,
    Guid MerchantId,
    string Type,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description,
    string IdempotencyKey,
    bool IsDuplicate);