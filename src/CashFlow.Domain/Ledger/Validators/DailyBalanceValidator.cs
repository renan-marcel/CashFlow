using FluentValidation;

namespace CashFlow.Domain.Ledger.Validators;

/// <summary>
/// Validador para a entidade DailyBalance
/// </summary>
public class DailyBalanceValidator : AbstractValidator<DailyBalance>
{
    public DailyBalanceValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId é obrigatório e não pode estar vazio");

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("A data é obrigatória")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A data não pode ser no futuro");

        RuleFor(x => x.Balance)
            .GreaterThanOrEqualTo(0)
            .WithMessage("O saldo não pode ser negativo");

        RuleFor(x => x.UpdatedAtUtc)
            .NotEmpty()
            .WithMessage("A data de atualização é obrigatória")
            .LessThanOrEqualTo(c => DateTime.UtcNow.AddSeconds(1))
            .WithMessage("A data de atualização não pode ser no futuro");
    }
}
