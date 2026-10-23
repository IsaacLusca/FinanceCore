using FinanceCore.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceCore.Api.Features.Operations;

[ApiController]
[Authorize]
[Route("api/accounts/{accountId:guid}/deposits")]
public sealed class DepositsController(OperationService operations) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OperationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OperationResponse>> Create(
        Guid accountId,
        DepositRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!IdempotencyKeys.TryNormalize(idempotencyKey, out var key))
        {
            ModelState.AddModelError(
                "Idempotency-Key",
                "Informe o cabeçalho Idempotency-Key com até 100 caracteres.");
            return ValidationProblem(ModelState);
        }

        if (!MoneyRules.IsValid(request.Amount))
        {
            ModelState.AddModelError(
                nameof(request.Amount),
                "O valor deve ser maior que zero, com no máximo duas casas decimais.");
            return ValidationProblem(ModelState);
        }

        var outcome = await operations.DepositAsync(User.GetUserId(), accountId, request.Amount, key, ct);

        return MapOutcome(outcome);
    }

    private ActionResult<OperationResponse> MapOutcome(OperationOutcome outcome) =>
        outcome.Status switch
        {
            OperationOutcomeStatus.Success =>
                StatusCode(StatusCodes.Status201Created, outcome.Operation),
            OperationOutcomeStatus.Replayed =>
                Ok(outcome.Operation),
            OperationOutcomeStatus.NotFound =>
                Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Conta não encontrada.",
                    detail: outcome.Detail),
            OperationOutcomeStatus.InsufficientFunds =>
                Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Saldo insuficiente.",
                    detail: outcome.Detail),
            OperationOutcomeStatus.LimitExceeded =>
                Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Limite excedido.",
                    detail: outcome.Detail),
            OperationOutcomeStatus.IdempotencyConflict =>
                Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflito de idempotência.",
                    detail: outcome.Detail),
            _ =>
                Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflito de concorrência.",
                    detail: outcome.Detail)
        };
}
