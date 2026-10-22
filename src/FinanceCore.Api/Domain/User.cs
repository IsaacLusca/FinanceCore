namespace FinanceCore.Api.Domain;

public sealed class User
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Account> Accounts { get; } = new List<Account>();
}
