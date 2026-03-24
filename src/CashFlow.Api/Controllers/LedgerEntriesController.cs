using Asp.Versioning;
using CashFlow.API.Contracts;
using CashFlow.Application.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ledger-entries")]
public sealed class LedgerEntriesController(ILedgerEntryApplicationService applicationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LedgerEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLedgerEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.MerchantId == Guid.Empty)
        {
            return ValidationProblem(title: "merchantId inválido", detail: "merchantId é obrigatório.");
        }

        var command = new CreateLedgerEntryCommand(
            request.MerchantId,
            request.Type,
            request.Amount,
            request.OccurredAt,
            request.Description,
            request.IdempotencyKey);

        var result = await applicationService.CreateAsync(command, cancellationToken);

        var response = new LedgerEntryResponse(
            result.LedgerEntryId,
            result.MerchantId,
            result.Type,
            result.Amount,
            result.OccurredAtUtc,
            result.Description,
            result.IdempotencyKey,
            result.IsDuplicate);

        return Created($"/api/v1/ledger-entries/{response.LedgerEntryId}", response);
    }
}