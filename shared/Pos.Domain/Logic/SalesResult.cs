namespace Pos.Domain.Logic;

// 計算項目 (api-design §3.12 の「計算」区分)。販売・返品で共通
public sealed record SalesResult
{
    public required IReadOnlyList<SalesResultLine> Lines { get; init; }

    public required IReadOnlyList<SalesResultDiscount> Discounts { get; init; }

    public required IReadOnlyList<SalesResultTaxSummary> TaxSummaries { get; init; }

    public required decimal Subtotal { get; init; }

    public required decimal DiscountTotal { get; init; }

    public required decimal NetSubtotal { get; init; }

    public required decimal TaxTotal { get; init; }

    public required decimal Total { get; init; }

    public required decimal TenderedTotal { get; init; }

    public required decimal ChangeAmount { get; init; }

    public required int PointsEarned { get; init; }

    public required int PointsRedeemed { get; init; }
}

public sealed record SalesResultLine
{
    public required Guid Id { get; init; }

    public required int LineNo { get; init; }

    public required decimal Amount { get; init; }

    public required decimal DiscountAmount { get; init; }

    public required decimal AllocatedDiscountAmount { get; init; }

    public required decimal NetAmount { get; init; }

    public required int PointsRedeemed { get; init; }

    public required int PointsEarned { get; init; }
}

public sealed record SalesResultDiscount
{
    public required Guid Id { get; init; }

    public required decimal Amount { get; init; }
}

public sealed record SalesResultTaxSummary
{
    public required Guid TaxRateId { get; init; }

    public required decimal Rate { get; init; }

    public required bool TaxIncluded { get; init; }

    public required decimal TaxableAmount { get; init; }

    public required decimal TaxAmount { get; init; }
}
