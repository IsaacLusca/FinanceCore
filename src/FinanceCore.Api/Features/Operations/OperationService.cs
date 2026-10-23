using FinanceCore.Api.Common;
using FinanceCore.Api.Data;
using FinanceCore.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinanceCore.Api.Features.Operations;

public sealed class OperationService(FinanceCoreDbContext db, ILogger<OperationService> logger)
{
    private const string IdempotencyConstraint = "ux_operations_user_idempotency_key";

    public async Task<OperationOutcome> DepositAsync(
        Guid userId,
        Guid accountId,
        decimal amount,
        string idempotencyKey,
        CancellationToken ct)
    {
        var existing = await FindExistingAsync(userId, idempotencyKey, ct);

        if (existing is not null)
        {
            return ReplayOrConflict(existing, OperationType.Deposit, amount, null, accountId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var account = await db.Accounts
                .SingleOrDefaultAsync(candidate => candidate.Id == accountId && candidate.UserId == userId, ct);

            if (account is null)
            {
                return OperationOutcome.Error(OperationOutcomeStatus.NotFound, "Conta não encontrada.");
            }

            if (account.Balance > MoneyRules.MaxAmount - amount)
            {
                return OperationOutcome.Error(
                    OperationOutcomeStatus.LimitExceeded,
                    "O limite de saldo da conta seria excedido.");
            }

            var now = DateTime.UtcNow;
            account.Balance += amount;

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Type = OperationType.Deposit,
                Amount = amount,
                DestinationAccountId = account.Id,
                InitiatedByUserId = userId,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = now
            };

            operation.Movements.Add(new Movement
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                AccountId = account.Id,
                Amount = amount,
                BalanceAfter = account.Balance,
                CreatedAtUtc = now
            });

            db.Operations.Add(operation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return OperationOutcome.Created(ToResponse(operation));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Conflito de concorrência no depósito da conta {AccountId}.", accountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Deposit, amount, null, accountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "A conta foi alterada por outra operação simultânea. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, IdempotencyConstraint))
        {
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Deposit, amount, null, accountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "Não foi possível concluir a operação. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsConcurrencyConflict(exception))
        {
            logger.LogWarning(exception, "Conflito transitório no depósito da conta {AccountId}.", accountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Deposit, amount, null, accountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "Conflito de concorrência. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsCheckViolation(exception))
        {
            logger.LogWarning(exception, "Restrição de saldo violada no depósito da conta {AccountId}.", accountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Deposit, amount, null, accountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "A operação violaria uma restrição de saldo. Tente novamente.");
        }
    }

    public async Task<OperationOutcome> TransferAsync(
        Guid userId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string idempotencyKey,
        CancellationToken ct)
    {
        var existing = await FindExistingAsync(userId, idempotencyKey, ct);

        if (existing is not null)
        {
            return ReplayOrConflict(
                existing,
                OperationType.Transfer,
                amount,
                sourceAccountId,
                destinationAccountId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var accounts = await db.Accounts
                .Where(candidate => candidate.Id == sourceAccountId || candidate.Id == destinationAccountId)
                .ToListAsync(ct);

            var source = accounts.SingleOrDefault(candidate => candidate.Id == sourceAccountId);
            var destination = accounts.SingleOrDefault(candidate => candidate.Id == destinationAccountId);

            if (source is null || source.UserId != userId)
            {
                return OperationOutcome.Error(OperationOutcomeStatus.NotFound, "Conta de origem não encontrada.");
            }

            if (destination is null)
            {
                return OperationOutcome.Error(OperationOutcomeStatus.NotFound, "Conta de destino não encontrada.");
            }

            if (source.Balance < amount)
            {
                return OperationOutcome.Error(
                    OperationOutcomeStatus.InsufficientFunds,
                    "Saldo insuficiente para a transferência.");
            }

            if (destination.Balance > MoneyRules.MaxAmount - amount)
            {
                return OperationOutcome.Error(
                    OperationOutcomeStatus.LimitExceeded,
                    "O limite de saldo da conta de destino seria excedido.");
            }

            var now = DateTime.UtcNow;
            source.Balance -= amount;
            destination.Balance += amount;

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Type = OperationType.Transfer,
                Amount = amount,
                SourceAccountId = source.Id,
                DestinationAccountId = destination.Id,
                InitiatedByUserId = userId,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = now
            };

            operation.Movements.Add(new Movement
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                AccountId = source.Id,
                Amount = -amount,
                BalanceAfter = source.Balance,
                CreatedAtUtc = now
            });

            operation.Movements.Add(new Movement
            {
                Id = Guid.NewGuid(),
                OperationId = operation.Id,
                AccountId = destination.Id,
                Amount = amount,
                BalanceAfter = destination.Balance,
                CreatedAtUtc = now
            });

            db.Operations.Add(operation);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return OperationOutcome.Created(ToResponse(operation));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                exception,
                "Conflito de concorrência na transferência da conta {SourceAccountId} para {DestinationAccountId}.",
                sourceAccountId,
                destinationAccountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Transfer, amount, sourceAccountId, destinationAccountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "Uma das contas foi alterada por outra operação simultânea. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, IdempotencyConstraint))
        {
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Transfer, amount, sourceAccountId, destinationAccountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "Não foi possível concluir a operação. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsConcurrencyConflict(exception))
        {
            logger.LogWarning(
                exception,
                "Conflito transitório na transferência da conta {SourceAccountId} para {DestinationAccountId}.",
                sourceAccountId,
                destinationAccountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Transfer, amount, sourceAccountId, destinationAccountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "Conflito de concorrência. Tente novamente.");
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsCheckViolation(exception))
        {
            logger.LogWarning(
                exception,
                "Restrição de saldo violada na transferência da conta {SourceAccountId} para {DestinationAccountId}.",
                sourceAccountId,
                destinationAccountId);
            await transaction.RollbackAsync(ct);
            return await TryReplayWinnerAsync(
                    userId, idempotencyKey, OperationType.Transfer, amount, sourceAccountId, destinationAccountId, ct)
                ?? OperationOutcome.Error(
                    OperationOutcomeStatus.ConcurrencyConflict,
                    "A operação violaria uma restrição de saldo. Tente novamente.");
        }
    }

    private Task<Operation?> FindExistingAsync(Guid userId, string idempotencyKey, CancellationToken ct) =>
        db.Operations
            .AsNoTracking()
            .Include(operation => operation.Movements)
            .SingleOrDefaultAsync(
                operation => operation.InitiatedByUserId == userId &&
                             operation.IdempotencyKey == idempotencyKey,
                ct);

    private async Task<OperationOutcome?> TryReplayWinnerAsync(
        Guid userId,
        string idempotencyKey,
        OperationType type,
        decimal amount,
        Guid? sourceAccountId,
        Guid? destinationAccountId,
        CancellationToken ct)
    {
        var winner = await FindExistingAsync(userId, idempotencyKey, ct);

        return winner is null
            ? null
            : ReplayOrConflict(winner, type, amount, sourceAccountId, destinationAccountId);
    }

    private static OperationOutcome ReplayOrConflict(
        Operation existing,
        OperationType type,
        decimal amount,
        Guid? sourceAccountId,
        Guid? destinationAccountId)
    {
        var sameRequest =
            existing.Type == type &&
            existing.Amount == amount &&
            existing.SourceAccountId == sourceAccountId &&
            existing.DestinationAccountId == destinationAccountId;

        return sameRequest
            ? OperationOutcome.Replay(ToResponse(existing))
            : OperationOutcome.Error(
                OperationOutcomeStatus.IdempotencyConflict,
                "A chave de idempotência já foi utilizada com parâmetros diferentes.");
    }

    private static OperationResponse ToResponse(Operation operation)
    {
        var sourceMovement = operation.SourceAccountId is { } sourceAccountId
            ? operation.Movements.SingleOrDefault(movement => movement.AccountId == sourceAccountId)
            : null;

        var destinationMovement = operation.DestinationAccountId is { } destinationAccountId
            ? operation.Movements.SingleOrDefault(movement => movement.AccountId == destinationAccountId)
            : null;

        return new OperationResponse(
            operation.Id,
            operation.Type,
            operation.Amount,
            operation.SourceAccountId,
            operation.DestinationAccountId,
            sourceMovement?.BalanceAfter,
            destinationMovement?.BalanceAfter,
            operation.CreatedAtUtc);
    }
}
