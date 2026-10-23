using FinanceCore.Api.Common;
using FinanceCore.Api.Data;
using FinanceCore.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceCore.Api.Features.Accounts;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(FinanceCoreDbContext db) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountResponse>> Create(CancellationToken ct)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = User.GetUserId(),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, ToResponse(account));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.CreatedAtUtc)
            .Select(account => new AccountResponse(account.Id, account.Balance, account.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(accounts);
    }

    [HttpGet("{accountId:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> Get(Guid accountId, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var account = await db.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId && candidate.UserId == userId, ct);

        if (account is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(account));
    }

    [HttpGet("{accountId:guid}/movements")]
    [ProducesResponseType<IReadOnlyList<MovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MovementResponse>>> Movements(
        Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            ModelState.AddModelError(nameof(page), "Use page >= 1 e pageSize entre 1 e 100.");
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserId();

        var accountExists = await db.Accounts
            .AnyAsync(account => account.Id == accountId && account.UserId == userId, ct);

        if (!accountExists)
        {
            return NotFound();
        }

        var movements = await db.Movements
            .AsNoTracking()
            .Where(movement => movement.AccountId == accountId)
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(movement => new MovementResponse(
                movement.Id,
                movement.OperationId,
                movement.Operation.Type,
                movement.Amount,
                movement.BalanceAfter,
                movement.Operation.Type == OperationType.Transfer
                    ? (movement.Operation.SourceAccountId == accountId
                        ? movement.Operation.DestinationAccountId
                        : movement.Operation.SourceAccountId)
                    : null,
                movement.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(movements);
    }

    private static AccountResponse ToResponse(Account account) =>
        new(account.Id, account.Balance, account.CreatedAtUtc);
}
