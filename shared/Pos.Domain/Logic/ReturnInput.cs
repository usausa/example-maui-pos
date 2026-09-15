namespace Pos.Domain.Logic;

// 返品の計算入力。値引は元取引から導出するので持たない
public sealed record ReturnInput
{
    public required TaxRounding TaxRounding { get; init; }

    // 元取引の明細 (全件)
    public required IReadOnlyList<ReturnOriginalLine> OriginalLines { get; init; }

    public required IReadOnlyList<ReturnInputLine> Lines { get; init; }

    public IReadOnlyList<SalesInputPayment> Payments { get; init; } = [];
}

public sealed record ReturnInputLine
{
    public required Guid Id { get; init; }

    public required int LineNo { get; init; }

    public required Guid OriginalLineId { get; init; }

    // 返品数量 (≤ 元数量 − 返品済み数量)
    public required decimal Quantity { get; init; }
}

// 元取引の明細
public sealed record ReturnOriginalLine
{
    public required Guid Id { get; init; }

    public required decimal UnitPrice { get; init; }

    public required decimal Quantity { get; init; }

    public decimal ReturnedQuantity { get; init; }

    public required decimal DiscountAmount { get; init; }

    public required decimal AllocatedDiscountAmount { get; init; }

    public required int PointsEarned { get; init; }

    public required int PointsRedeemed { get; init; }

    public required Guid TaxRateId { get; init; }

    public required decimal TaxRate { get; init; }

    public required bool TaxIncluded { get; init; }
}
