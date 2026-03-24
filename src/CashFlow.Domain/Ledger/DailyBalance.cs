namespace CashFlow.Domain.Ledger;

/// <summary>
/// Represents the daily balance for a merchant.
/// </summary>
/// <remarks>
/// This entity tracks the cumulative balance for a specific merchant on a given date.
/// Balance updates are recorded through ApplyCredit and ApplyDebit operations.
/// Validation of merchant ID, date, balance, and amount values is delegated to DailyBalanceValidator.
/// </remarks>
public sealed class DailyBalance : Entity
{
    /// <summary>
    /// Gets the merchant identifier.
    /// </summary>
    public Guid MerchantId { get; private set; }

    /// <summary>
    /// Gets the date for which this balance is recorded.
    /// </summary>
    public DateOnly Date { get; private set; }

    /// <summary>
    /// Gets the current balance amount.
    /// </summary>
    public decimal Balance { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the last update to this balance.
    /// </summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyBalance"/> class.
    /// </summary>
    private DailyBalance()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyBalance"/> class with the specified merchant and date.
    /// </summary>
    /// <param name="merchantId">The merchant identifier.</param>
    /// <param name="date">The date for which the balance is being created.</param>
    /// <remarks>
    /// The initial balance is set to zero. Validation of merchant ID and date is delegated to DailyBalanceValidator.
    /// </remarks>
    public DailyBalance(Guid merchantId, DateOnly date)
    {
        MerchantId = merchantId;
        Date = date;
        Balance = 0;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a credit (positive amount) to the balance and updates the timestamp.
    /// </summary>
    /// <param name="amount">The credit amount to apply.</param>
    /// <remarks>
    /// Validation that the amount is greater than zero is delegated to DailyBalanceValidator.
    /// </remarks>
    public void ApplyCredit(decimal amount)
    {
        Balance += amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a debit (negative amount) to the balance and updates the timestamp.
    /// </summary>
    /// <param name="amount">The debit amount to apply.</param>
    /// <remarks>
    /// Validation that the amount is greater than zero is delegated to DailyBalanceValidator.
    /// </remarks>
    public void ApplyDebit(decimal amount)
    {
        Balance -= amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}