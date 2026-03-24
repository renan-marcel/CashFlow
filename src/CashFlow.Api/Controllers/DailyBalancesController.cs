using Asp.Versioning;
using CashFlow.API.Contracts;
using CashFlow.Application.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/daily-balances")]
public sealed class DailyBalancesController(IDailyBalanceQueryService queryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DailyBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] Guid merchantId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        if (merchantId == Guid.Empty)
        {
            return ValidationProblem(title: "merchantId inválido", detail: "merchantId é obrigatório.");
        }

        var result = await queryService.GetAsync(new GetDailyBalanceQuery(merchantId, date), cancellationToken);
        if (result is null)
        {
            return NotFound(Problem(title: "Saldo não encontrado", detail: "Não há saldo consolidado para os filtros informados."));
        }

        return Ok(new DailyBalanceResponse(result.MerchantId, result.Date, result.Balance, result.UpdatedAtUtc));
    }
}