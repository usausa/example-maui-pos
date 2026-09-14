namespace Pos.Domain.Logic;

// api-design §4.6 の計算例 (デジカメ + SD カード × 2 + 配送料、取引値引 1,000、5,000 pt 利用)
internal static class SalesExample
{
    public static readonly Guid TaxRate10 = new("00000000-0000-0000-0000-000000000010");

    public static readonly Guid TaxRate8 = new("00000000-0000-0000-0000-000000000008");

    public static readonly Guid CameraProduct = new("00000000-0000-0000-0001-000000000001");

    public static readonly Guid SdCardProduct = new("00000000-0000-0000-0001-000000000002");

    public static readonly Guid DeliveryProduct = new("00000000-0000-0000-0001-000000000003");

    public static readonly Guid CameraLine = new("00000000-0000-0000-0002-000000000001");

    public static readonly Guid SdCardLine = new("00000000-0000-0000-0002-000000000002");

    public static readonly Guid DeliveryLine = new("00000000-0000-0000-0002-000000000003");

    public static readonly Guid CameraDiscount = new("00000000-0000-0000-0003-000000000001");

    public static readonly Guid TransactionDiscount = new("00000000-0000-0000-0003-000000000002");

    public static readonly Guid PointsPayment = new("00000000-0000-0000-0004-000000000001");

    public static readonly Guid CardPayment = new("00000000-0000-0000-0004-000000000002");

    public static readonly Guid CashPayment = new("00000000-0000-0000-0004-000000000003");

    public static SalesInput Input() => new()
    {
        TaxRounding = TaxRounding.Floor,
        PointBasis = PointBasis.TaxIncluded,
        Lines = Lines(),
        Discounts = Discounts(),
        Payments = Payments()
    };

    public static List<SalesInputLine> Lines() =>
    [
        Line(CameraLine, 1, CameraProduct, 80000m, 1m, 0.10m),
        Line(SdCardLine, 2, SdCardProduct, 2000m, 2m, 0.01m),
        Line(DeliveryLine, 3, DeliveryProduct, 1100m, 1m, 0m)
    ];

    public static List<SalesInputDiscount> Discounts() =>
    [
        new() { Id = CameraDiscount, LineId = CameraLine, Type = DiscountType.Percent, Value = 0.05m },
        new() { Id = TransactionDiscount, LineId = null, Type = DiscountType.Amount, Value = 1000m }
    ];

    public static List<SalesInputPayment> Payments() =>
    [
        new() { Id = PointsPayment, Kind = PaymentKind.Points, Amount = 5000m, TenderedAmount = 5000m },
        new() { Id = CardPayment, Kind = PaymentKind.Card, Amount = 50000m, TenderedAmount = 50000m },
        new() { Id = CashPayment, Kind = PaymentKind.Cash, Amount = 25100m, TenderedAmount = 30000m, AllowsChange = true }
    ];

    public static SalesInputLine Line(Guid id, int lineNo, Guid productId, decimal unitPrice, decimal quantity, decimal pointRate, Guid? taxRateId = null, decimal taxRate = 0.10m, bool taxIncluded = true) => new()
    {
        Id = id,
        LineNo = lineNo,
        ProductId = productId,
        ListPrice = unitPrice,
        UnitPrice = unitPrice,
        Quantity = quantity,
        TaxRateId = taxRateId ?? TaxRate10,
        TaxRate = taxRate,
        TaxIncluded = taxIncluded,
        PointRate = pointRate
    };

    public static SalesInputPayment Cash(decimal amount, decimal? tendered = null) => new()
    {
        Id = CashPayment,
        Kind = PaymentKind.Cash,
        Amount = amount,
        TenderedAmount = tendered ?? amount,
        AllowsChange = true
    };

    // §4.6 の元取引の明細 (返品テスト用)
    public static List<ReturnOriginalLine> OriginalLines() =>
    [
        Original(CameraLine, 80000m, 1m, 4000m, 937m, 7037, 4685),
        Original(SdCardLine, 2000m, 2m, 0m, 49m, 37, 247),
        Original(DeliveryLine, 1100m, 1m, 0m, 14m, 0, 68)
    ];

    private static ReturnOriginalLine Original(Guid id, decimal unitPrice, decimal quantity, decimal discountAmount, decimal allocatedDiscountAmount, int pointsEarned, int pointsRedeemed) => new()
    {
        Id = id,
        UnitPrice = unitPrice,
        Quantity = quantity,
        DiscountAmount = discountAmount,
        AllocatedDiscountAmount = allocatedDiscountAmount,
        PointsEarned = pointsEarned,
        PointsRedeemed = pointsRedeemed,
        TaxRateId = TaxRate10,
        TaxRate = 0.10m,
        TaxIncluded = true
    };
}
