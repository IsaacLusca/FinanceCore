namespace FinanceCore.Api.Domain;

public sealed class Operation
{
    public Guid Id { get; set; }

    public OperationType Type { get; set; }

    public decimal Amount { get; set; }

    public Guid? SourceAccountId { get; set; }

    public Account? SourceAccount { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public Account? DestinationAccount { get; set; }

    public Guid InitiatedByUserId { get; set; }

    public User InitiatedByUser { get; set; } = null!;

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Movement> Movements { get; } = new List<Movement>();
}
