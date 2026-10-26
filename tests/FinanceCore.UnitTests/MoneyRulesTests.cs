using FinanceCore.Api.Common;

namespace FinanceCore.UnitTests;

public sealed class MoneyRulesTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(1.23)]
    public void ValidAmountsWithUpToTwoDecimalsAreAccepted(double value)
    {
        Assert.True(MoneyRules.IsValid((decimal)value));
    }

    [Fact]
    public void MaxAmountIsAccepted()
    {
        Assert.True(MoneyRules.IsValid(MoneyRules.MaxAmount));
    }

    [Fact]
    public void AmountWithTrailingZerosIsAccepted()
    {
        Assert.True(MoneyRules.IsValid(1.230m));
    }

    [Fact]
    public void AmountAboveMaxIsRejected()
    {
        Assert.False(MoneyRules.IsValid(MoneyRules.MaxAmount + 0.01m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1.005)]
    [InlineData(1.999)]
    [InlineData(10000000000000000.0)]
    public void InvalidAmountsAreRejected(double value)
    {
        Assert.False(MoneyRules.IsValid((decimal)value));
    }
}
