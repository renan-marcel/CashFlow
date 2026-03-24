namespace CashFlow.API.Contracts;

public sealed class CreateLedgerEntryRequest
{
    public Guid MerchantId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Description { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}