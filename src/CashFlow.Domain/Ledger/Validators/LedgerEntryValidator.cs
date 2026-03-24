using FluentValidation;

namespace CashFlow.Domain.Ledger.Validators;

/// <summary>
/// Validador para a entidade LedgerEntry
/// </summary>
public class LedgerEntryValidator : AbstractValidator<LedgerEntry>
{
    public LedgerEntryValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId é obrigatório e não pode estar vazio");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Tipo de lançamento inválido");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("O valor deve ser maior que zero")
            .LessThanOrEqualTo(decimal.MaxValue)
            .WithMessage("O valor excede o limite máximo permitido");

        RuleFor(x => x.OccurredAtUtc)
            .NotEmpty()
            .WithMessage("A data do lançamento é obrigatória")
            .LessThanOrEqualTo(c => DateTime.UtcNow.AddSeconds(1))
            .WithMessage("A data do lançamento não pode ser no futuro");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("A descrição não pode ter mais de 500 caracteres");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey é obrigatório")
            .MinimumLength(1)
            .WithMessage("IdempotencyKey não pode estar em branco")
            .MaximumLength(256)
            .WithMessage("IdempotencyKey não pode ter mais de 256 caracteres");
    }
}
