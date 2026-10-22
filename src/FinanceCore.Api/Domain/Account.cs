namespace FinanceCore.Api.Domain;

public sealed class Account
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public decimal Balance { get; set; }

    public uint Version { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
