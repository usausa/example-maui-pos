namespace Pos.Shared.Reports;

// 売上集計 (GET /reports/sales/summary)。取消済みは除外し、返品は負として扱う
public sealed class SalesSummaryResponse
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    // day / hour / terminal / staff / paymentMethod / taxRate / category
    public string GroupBy { get; set; } = default!;

    public IReadOnlyList<SalesSummaryResponseRow> Rows { get; set; } = default!;

    public SalesSummaryResponseRow Total { get; set; } = default!;
}

public sealed class SalesSummaryResponseRow
{
    public string Key { get; set; } = default!;

    public string Label { get; set; } = default!;

    public int TransactionCount { get; set; }

    public int ReturnCount { get; set; }

    public int CustomerCount { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }

    public decimal NetSales { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    // groupBy = taxRate のみ
    public decimal? TaxableAmount { get; set; }

    public decimal? TaxAmount { get; set; }
}
