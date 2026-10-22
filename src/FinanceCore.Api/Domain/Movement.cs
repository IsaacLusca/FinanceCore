namespace FinanceCore.Api.Domain;

public sealed class Movement
{
    public Guid Id { get; set; }

    public Guid OperationId { get; set; }

    public Operation Operation { get; set; } = null!;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
