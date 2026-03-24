namespace CashFlow.Application.Ledger;

public interface ILedgerEntryApplicationService
{
    Task<CreateLedgerEntryResult> CreateAsync(CreateLedgerEntryCommand command, CancellationToken cancellationToken);
}