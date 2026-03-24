namespace CashFlow.Domain.Ledger;

/// <summary>
/// Representa uma entrada no livro razão de um comerciante
/// </summary>
public sealed class LedgerEntry : Entity
{
    /// <summary>
    /// ID do comerciante associado a esta entrada
    /// </summary>
    public Guid MerchantId { get; private set; }

    /// <summary>
    /// Tipo da entrada (Crédito ou Débito)
    /// </summary>
    public LedgerEntryType Type { get; private set; }

    /// <summary>
    /// Valor da transação
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Data e hora UTC quando a transação ocorreu
    /// </summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Descrição opcional da transação
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Chave de idempotência para garantir unicidade
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    private LedgerEntry()
    {
    }

    /// <summary>
    /// Cria uma nova entrada no livro razão
    /// </summary>
    /// <remarks>
    /// A validação deve ser realizada usando LedgerEntryValidator antes de persistir
    /// </remarks>
    public LedgerEntry(
        Guid merchantId,
        LedgerEntryType type,
        decimal amount,
        DateTime occurredAtUtc,
        string? description,
        string idempotencyKey)
    {
        MerchantId = merchantId;
        Type = type;
        Amount = amount;
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        Description = description;
        IdempotencyKey = idempotencyKey.Trim();
    }
}
