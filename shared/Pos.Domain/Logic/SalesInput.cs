namespace Pos.Domain.Logic;

// 販売の計算入力 (api-design §4)。Request / Response とは別の純粋な型
public sealed record SalesInput
{
    public required TaxRounding TaxRounding { get; init; }

    public required PointBasis PointBasis { get; init; }

    public required IReadOnlyList<SalesInputLine> Lines { get; init; }

    public IReadOnlyList<SalesInputDiscount> Discounts { get; init; } = [];

    public IReadOnlyList<SalesInputPayment> Payments { get; init; } = [];
}

public sealed record SalesInputLine
{
    public required Guid Id { get; init; }

    public required int LineNo { get; init; }

    public required Guid ProductId { get; init; }

    // 販売時点のマスタ価格
    public required decimal ListPrice { get; init; }

    public required decimal UnitPrice { get; init; }

    public required decimal Quantity { get; init; }

    public required Guid TaxRateId { get; init; }

    public required decimal TaxRate { get; init; }

    public required bool TaxIncluded { get; init; }

    public decimal PointRate { get; init; }
}

public sealed record SalesInputDiscount
{
    public required Guid Id { get; init; }

    // null = 取引値引
    public Guid? LineId { get; init; }

    public required DiscountType Type { get; init; }

    public required decimal Value { get; init; }
}

public sealed record SalesInputPayment
{
    public required Guid Id { get; init; }

    public required PaymentKind Kind { get; init; }

    public required decimal Amount { get; init; }

    public required decimal TenderedAmount { get; init; }

    public bool AllowsChange { get; init; }
}
