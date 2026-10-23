using FinanceCore.Api.Domain;

namespace FinanceCore.Api.Features.Operations;

public sealed record DepositRequest(decimal Amount);

public sealed record TransferRequest(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount);

public sealed record OperationResponse(
    Guid Id,
    OperationType Type,
    decimal Amount,
    Guid? SourceAccountId,
    Guid? DestinationAccountId,
    decimal? SourceBalanceAfter,
    decimal? DestinationBalanceAfter,
    DateTime CreatedAtUtc);

public enum OperationOutcomeStatus
{
    Success,
    Replayed,
    NotFound,
    InsufficientFunds,
    LimitExceeded,
    IdempotencyConflict,
    ConcurrencyConflict
}

public sealed record OperationOutcome(
    OperationOutcomeStatus Status,
    OperationResponse? Operation = null,
    string? Detail = null)
{
    public static OperationOutcome Created(OperationResponse operation) =>
        new(OperationOutcomeStatus.Success, operation);

    public static OperationOutcome Replay(OperationResponse operation) =>
        new(OperationOutcomeStatus.Replayed, operation);

    public static OperationOutcome Error(OperationOutcomeStatus status, string detail) =>
        new(status, null, detail);
}
