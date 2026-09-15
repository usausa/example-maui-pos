namespace Pos.Domain.Logic;

public sealed class SalesLogicTests
{
    // 計算例。全数値が一致すること
    [Fact]
    public void CalculateExample()
    {
        var result = SalesLogic.Calculate(SalesExample.Input());

        Assert.Equal(3, result.Lines.Count);

        var camera = result.Lines[0];
        Assert.Equal(SalesExample.CameraLine, camera.Id);
        Assert.Equal(80000m, camera.Amount);
        Assert.Equal(4000m, camera.DiscountAmount);
        Assert.Equal(937m, camera.AllocatedDiscountAmount);
        Assert.Equal(75063m, camera.NetAmount);
        Assert.Equal(4685, camera.PointsRedeemed);
        Assert.Equal(7037, camera.PointsEarned);

        var sdCard = result.Lines[1];
        Assert.Equal(4000m, sdCard.Amount);
        Assert.Equal(0m, sdCard.DiscountAmount);
        Assert.Equal(49m, sdCard.AllocatedDiscountAmount);
        Assert.Equal(3951m, sdCard.NetAmount);
        Assert.Equal(247, sdCard.PointsRedeemed);
        Assert.Equal(37, sdCard.PointsEarned);

        var delivery = result.Lines[2];
        Assert.Equal(1100m, delivery.Amount);
        Assert.Equal(0m, delivery.DiscountAmount);
        Assert.Equal(14m, delivery.AllocatedDiscountAmount);
        Assert.Equal(1086m, delivery.NetAmount);
        Assert.Equal(68, delivery.PointsRedeemed);
        Assert.Equal(0, delivery.PointsEarned);

        Assert.Equal(2, result.Discounts.Count);
        Assert.Equal(4000m, result.Discounts[0].Amount);
        Assert.Equal(1000m, result.Discounts[1].Amount);

        var summary = Assert.Single(result.TaxSummaries);
        Assert.Equal(SalesExample.TaxRate10, summary.TaxRateId);
        Assert.Equal(0.10m, summary.Rate);
        Assert.True(summary.TaxIncluded);
        Assert.Equal(80100m, summary.TaxableAmount);
        Assert.Equal(7281m, summary.TaxAmount);

        Assert.Equal(85100m, result.Subtotal);
        Assert.Equal(5000m, result.DiscountTotal);
        Assert.Equal(80100m, result.NetSubtotal);
        Assert.Equal(7281m, result.TaxTotal);
        Assert.Equal(80100m, result.Total);
        Assert.Equal(85000m, result.TenderedTotal);
        Assert.Equal(4900m, result.ChangeAmount);
        Assert.Equal(7074, result.PointsEarned);
        Assert.Equal(5000, result.PointsRedeemed);
    }

    // 内税 / 外税の混在と税率複数。グループごとに集計してから税額を計算する
    [Fact]
    public void CalculateMixedTaxGroups()
    {
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = PointBasis.TaxIncluded,
            Lines =
            [
                SalesExample.Line(SalesExample.CameraLine, 1, SalesExample.CameraProduct, 1000m, 1m, 0m, taxIncluded: false),
                SalesExample.Line(SalesExample.SdCardLine, 2, SalesExample.SdCardProduct, 1080m, 1m, 0m, SalesExample.TaxRate8, 0.08m),
                SalesExample.Line(SalesExample.DeliveryLine, 3, SalesExample.DeliveryProduct, 500m, 2m, 0m, taxIncluded: false)
            ],
            Payments = [SalesExample.Cash(3280m)]
        };

        var result = SalesLogic.Calculate(input);

        Assert.Equal(2, result.TaxSummaries.Count);

        var excluded = result.TaxSummaries[0];
        Assert.Equal(SalesExample.TaxRate10, excluded.TaxRateId);
        Assert.False(excluded.TaxIncluded);
        Assert.Equal(2000m, excluded.TaxableAmount);
        Assert.Equal(200m, excluded.TaxAmount);

        var included = result.TaxSummaries[1];
        Assert.Equal(SalesExample.TaxRate8, included.TaxRateId);
        Assert.True(included.TaxIncluded);
        Assert.Equal(1080m, included.TaxableAmount);
        Assert.Equal(80m, included.TaxAmount);

        Assert.Equal(3080m, result.Subtotal);
        Assert.Equal(0m, result.DiscountTotal);
        Assert.Equal(3080m, result.NetSubtotal);
        Assert.Equal(280m, result.TaxTotal);
        Assert.Equal(3280m, result.Total);
        Assert.Equal(0m, result.ChangeAmount);
    }

    // ポイント基準: 外税明細は TaxIncluded なら按分税額を加え、内税明細は TaxExcluded なら按分税額を引く
    [Theory]
    [InlineData(PointBasis.TaxIncluded, false, 110)]
    [InlineData(PointBasis.TaxExcluded, false, 100)]
    [InlineData(PointBasis.TaxIncluded, true, 110)]
    [InlineData(PointBasis.TaxExcluded, true, 100)]
    public void CalculatePointBasis(PointBasis basis, bool taxIncluded, int expectedPoints)
    {
        var unitPrice = taxIncluded ? 1100m : 1000m;
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = basis,
            Lines = [SalesExample.Line(SalesExample.CameraLine, 1, SalesExample.CameraProduct, unitPrice, 1m, 0.10m, taxIncluded: taxIncluded)],
            Payments = [SalesExample.Cash(1100m)]
        };

        var result = SalesLogic.Calculate(input);

        Assert.Equal(1100m, result.Total);
        Assert.Equal(expectedPoints, result.PointsEarned);
    }

    // 同じ税グループ内の按分税額は netAmount 比で配られる
    [Fact]
    public void CalculatePointBasisWithAllocatedTax()
    {
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = PointBasis.TaxIncluded,
            Lines =
            [
                SalesExample.Line(SalesExample.CameraLine, 1, SalesExample.CameraProduct, 1000m, 1m, 0.10m, taxIncluded: false),
                SalesExample.Line(SalesExample.SdCardLine, 2, SalesExample.SdCardProduct, 3000m, 1m, 0.10m, taxIncluded: false)
            ],
            Payments = [SalesExample.Cash(4400m)]
        };

        var result = SalesLogic.Calculate(input);

        Assert.Equal(400m, result.TaxTotal);
        Assert.Equal(110, result.Lines[0].PointsEarned);
        Assert.Equal(330, result.Lines[1].PointsEarned);
    }

    // 数量が小数のときの明細金額は切り捨て
    [Fact]
    public void CalculateFractionalQuantity()
    {
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = PointBasis.TaxIncluded,
            Lines = [SalesExample.Line(SalesExample.CameraLine, 1, SalesExample.CameraProduct, 333m, 1.5m, 0m)],
            Payments = [SalesExample.Cash(499m)]
        };

        var result = SalesLogic.Calculate(input);

        Assert.Equal(499m, result.Lines[0].Amount);
        Assert.Equal(499m, result.Total);
    }

    // 税の丸めは会社設定に従う
    [Theory]
    [InlineData(TaxRounding.Floor, 100)]
    [InlineData(TaxRounding.Round, 101)]
    [InlineData(TaxRounding.Ceiling, 101)]
    public void CalculateTaxRounding(TaxRounding rounding, int expectedTax)
    {
        var input = new SalesInput
        {
            TaxRounding = rounding,
            PointBasis = PointBasis.TaxIncluded,
            Lines = [SalesExample.Line(SalesExample.CameraLine, 1, SalesExample.CameraProduct, 1005m, 1m, 0m, taxIncluded: false)],
            Payments = [SalesExample.Cash(1005m + expectedTax)]
        };

        var result = SalesLogic.Calculate(input);

        Assert.Equal(expectedTax, result.TaxTotal);
        Assert.Equal(1005m + expectedTax, result.Total);
    }

    // 取引値引の率は明細値引後の合計に対して計算する
    [Fact]
    public void CalculateTransactionPercentDiscount()
    {
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = PointBasis.TaxIncluded,
            Lines = SalesExample.Lines(),
            Discounts =
            [
                new() { Id = SalesExample.CameraDiscount, LineId = SalesExample.CameraLine, Type = DiscountType.Amount, Value = 100m },
                new() { Id = SalesExample.TransactionDiscount, LineId = null, Type = DiscountType.Percent, Value = 0.10m }
            ],
            Payments = [SalesExample.Cash(76500m)]
        };

        var result = SalesLogic.Calculate(input);

        // (85,100 − 100) × 10% = 8,500
        Assert.Equal(8500m, result.Discounts[1].Amount);
        Assert.Equal(8600m, result.DiscountTotal);
        Assert.Equal(76500m, result.NetSubtotal);
        Assert.Equal(76500m, result.Total);
    }

    // 値引の対象明細が存在しない入力は例外
    [Fact]
    public void CalculateThrowsForUnknownDiscountLine()
    {
        var input = new SalesInput
        {
            TaxRounding = TaxRounding.Floor,
            PointBasis = PointBasis.TaxIncluded,
            Lines = SalesExample.Lines(),
            Discounts = [new() { Id = SalesExample.CameraDiscount, LineId = Guid.NewGuid(), Type = DiscountType.Amount, Value = 100m }]
        };

        Assert.Throws<ArgumentException>(() => SalesLogic.Calculate(input));
    }
}
