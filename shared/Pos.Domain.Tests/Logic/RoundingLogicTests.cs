namespace Pos.Domain.Logic;

public sealed class RoundingLogicTests
{
    // 端数処理 (切り捨て、四捨五入、切り上げ)
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

    // 定義にない端数処理は例外
    [Fact]
    public void ApplyThrowsForUndefinedRounding()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RoundingLogic.Apply(1m, (TaxRounding)99));
    }
}
