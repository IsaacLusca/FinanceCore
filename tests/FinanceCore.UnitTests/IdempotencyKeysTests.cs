using FinanceCore.Api.Common;

namespace FinanceCore.UnitTests;

public sealed class IdempotencyKeysTests
{
    [Fact]
    public void KeyIsTrimmedAndAccepted()
    {
        var accepted = IdempotencyKeys.TryNormalize("  chave-123  ", out var normalized);

        Assert.True(accepted);
        Assert.Equal("chave-123", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyKeysAreRejected(string? value)
    {
        Assert.False(IdempotencyKeys.TryNormalize(value, out _));
    }

    [Fact]
    public void KeyAtMaximumLengthIsAccepted()
    {
        var key = new string('a', IdempotencyKeys.MaxLength);

        Assert.True(IdempotencyKeys.TryNormalize(key, out var normalized));
        Assert.Equal(key, normalized);
    }

    [Fact]
    public void KeyLongerThanMaximumIsRejected()
    {
        var key = new string('a', IdempotencyKeys.MaxLength + 1);

        Assert.False(IdempotencyKeys.TryNormalize(key, out _));
    }
}
