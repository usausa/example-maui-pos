namespace Pos.Domain.Sales;

public sealed class RoundingTests
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
        Assert.Equal(expected, Rounding.Apply((decimal)value, rounding));
    }

    [Fact]
    public void ApplyThrowsForUndefinedRounding()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rounding.Apply(1m, (TaxRounding)99));
    }
}
