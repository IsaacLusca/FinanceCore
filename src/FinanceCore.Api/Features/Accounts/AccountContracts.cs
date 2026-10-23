using FinanceCore.Api.Domain;

namespace FinanceCore.Api.Features.Accounts;

public sealed record AccountResponse(Guid Id, decimal Balance, DateTime CreatedAtUtc);

public sealed record MovementResponse(
    Guid Id,
    Guid OperationId,
    OperationType Type,
    decimal Amount,
    decimal BalanceAfter,
    Guid? CounterpartyAccountId,
    DateTime CreatedAtUtc);
