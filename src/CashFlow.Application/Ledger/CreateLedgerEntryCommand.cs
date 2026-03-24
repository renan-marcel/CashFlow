namespace CashFlow.Application.Ledger;

public sealed record CreateLedgerEntryCommand(
    Guid MerchantId,
    string Type,
    decimal Amount,
    DateTime OccurredAt,
    string? Description,
    string IdempotencyKey);