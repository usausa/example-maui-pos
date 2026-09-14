namespace Pos.Domain.Logic;

public sealed class RoundingLogicTests
{
    [Theory]
    [InlineData(TaxRounding.Floor, 100.9, 100)]
    [InlineData(TaxRounding.Floor, 100.0, 100)]
    [InlineData(TaxRounding.Round, 100.4, 100)]
    [InlineData(TaxRounding.Round, 100.5, 101)]
    [InlineData(TaxRounding.Round, 101.5, 102)]
    [InlineData(TaxRounding.Ceiling, 100.1, 101)]
    [InlineData(TaxRounding.Ceiling, 100.0, 100)]
    public void Apply(TaxRounding rounding, double value, int expected)
    {
        Assert.Equal(expected, RoundingLogic.Apply((decimal)value, rounding));
    }

    [Fact]
    public void ApplyThrowsForUndefinedRounding()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoundingLogic.Apply(1m, (TaxRounding)99));
    }
}
