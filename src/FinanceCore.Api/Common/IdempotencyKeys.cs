namespace FinanceCore.Api.Common;

public static class IdempotencyKeys
{
    public const int MaxLength = 100;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= MaxLength;
    }
}
