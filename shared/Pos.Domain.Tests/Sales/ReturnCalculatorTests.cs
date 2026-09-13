namespace Pos.Domain.Sales;

public sealed class ReturnCalculatorTests
{
    private static readonly Guid ReturnLine1 = new("00000000-0000-0000-0005-000000000001");

    private static readonly Guid ReturnLine2 = new("00000000-0000-0000-0005-000000000002");

    private static readonly Guid ReturnLine3 = new("00000000-0000-0000-0005-000000000003");

    // SD カード 2 個のうち 1 個を返品。値引・ポイントは数量比で切り捨て
    [Fact]
    public void CalculatePartialReturn()
    {
        var input = new ReturnInput
        {
            TaxRounding = TaxRounding.Floor,
            OriginalLines = SalesExample.OriginalLines(),
            Lines = [new() { Id = ReturnLine1, LineNo = 1, OriginalLineId = SalesExample.SdCardLine, Quantity = 1m }],
            Payments =
            [
                new() { Id = SalesExample.PointsPayment, Kind = PaymentKind.Points, Amount = 123m, TenderedAmount = 123m },
                new() { Id = SalesExample.CashPayment, Kind = PaymentKind.Cash, Amount = 1853m, TenderedAmount = 1853m, AllowsChange = true }
            ]
        };

        var result = ReturnCalculator.Calculate(input);

        var line = Assert.Single(result.Lines);
        Assert.Equal(ReturnLine1, line.Id);
        Assert.Equal(2000m, line.Amount);
        Assert.Equal(0m, line.DiscountAmount);
        Assert.Equal(24m, line.AllocatedDiscountAmount);
        Assert.Equal(1976m, line.NetAmount);
        Assert.Equal(-18, line.PointsEarned);
        Assert.Equal(-123, line.PointsRedeemed);

        var summary = Assert.Single(result.TaxSummaries);
        Assert.Equal(1976m, summary.TaxableAmount);
        Assert.Equal(179m, summary.TaxAmount);

        Assert.Empty(result.Discounts);
        Assert.Equal(2000m, result.Subtotal);
        Assert.Equal(24m, result.DiscountTotal);
        Assert.Equal(1976m, result.NetSubtotal);
        Assert.Equal(179m, result.TaxTotal);
        Assert.Equal(1976m, result.Total);
        Assert.Equal(1976m, result.TenderedTotal);
        Assert.Equal(0m, result.ChangeAmount);
        Assert.Equal(-18, result.PointsEarned);
        Assert.Equal(-123, result.PointsRedeemed);
    }

    // 全数量の返品は元取引と同額になる
    [Fact]
    public void CalculateFullReturnEqualsOriginal()
    {
        var original = SalesCalculator.Calculate(SalesExample.Input());

        var input = new ReturnInput
        {
            TaxRounding = TaxRounding.Floor,
            OriginalLines = SalesExample.OriginalLines(),
            Lines =
            [
                new() { Id = ReturnLine1, LineNo = 1, OriginalLineId = SalesExample.CameraLine, Quantity = 1m },
                new() { Id = ReturnLine2, LineNo = 2, OriginalLineId = SalesExample.SdCardLine, Quantity = 2m },
                new() { Id = ReturnLine3, LineNo = 3, OriginalLineId = SalesExample.DeliveryLine, Quantity = 1m }
            ],
            Payments =
            [
                new() { Id = SalesExample.PointsPayment, Kind = PaymentKind.Points, Amount = 5000m, TenderedAmount = 5000m },
                new() { Id = SalesExample.CashPayment, Kind = PaymentKind.Cash, Amount = 75100m, TenderedAmount = 75100m, AllowsChange = true }
            ]
        };

        var result = ReturnCalculator.Calculate(input);

        Assert.Equal(original.Subtotal, result.Subtotal);
        Assert.Equal(original.DiscountTotal, result.DiscountTotal);
        Assert.Equal(original.NetSubtotal, result.NetSubtotal);
        Assert.Equal(original.TaxTotal, result.TaxTotal);
        Assert.Equal(original.Total, result.Total);
        Assert.Equal(-original.PointsEarned, result.PointsEarned);
        Assert.Equal(-original.PointsRedeemed, result.PointsRedeemed);
        for (var i = 0; i < original.Lines.Count; i++)
        {
            Assert.Equal(original.Lines[i].Amount, result.Lines[i].Amount);
            Assert.Equal(original.Lines[i].DiscountAmount, result.Lines[i].DiscountAmount);
            Assert.Equal(original.Lines[i].AllocatedDiscountAmount, result.Lines[i].AllocatedDiscountAmount);
            Assert.Equal(original.Lines[i].NetAmount, result.Lines[i].NetAmount);
            Assert.Equal(-original.Lines[i].PointsEarned, result.Lines[i].PointsEarned);
            Assert.Equal(-original.Lines[i].PointsRedeemed, result.Lines[i].PointsRedeemed);
        }
    }

    [Fact]
    public void CalculateThrowsForUnknownOriginalLine()
    {
        var input = new ReturnInput
        {
            TaxRounding = TaxRounding.Floor,
            OriginalLines = SalesExample.OriginalLines(),
            Lines = [new() { Id = ReturnLine1, LineNo = 1, OriginalLineId = Guid.NewGuid(), Quantity = 1m }]
        };

        Assert.Throws<ArgumentException>(() => ReturnCalculator.Calculate(input));
    }
}
