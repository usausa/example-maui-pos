namespace Pos.Domain.Sales;

public static class Rounding
{
    public static decimal Apply(decimal value, TaxRounding rounding) =>
        rounding switch
        {
            TaxRounding.Floor => Math.Floor(value),
            TaxRounding.Round => Math.Round(value, MidpointRounding.AwayFromZero),
            TaxRounding.Ceiling => Math.Ceiling(value),
            _ => throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null)
        };
}
