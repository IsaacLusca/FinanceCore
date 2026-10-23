namespace FinanceCore.Api.Common;

public static class MoneyRules
{
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public static bool IsValid(decimal amount) =>
        amount > 0 &&
        amount <= MaxAmount &&
        decimal.Round(amount, 2) == amount;
}
