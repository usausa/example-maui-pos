namespace Pos.Contract.Reports;

// 商品別売上 (GET /reports/sales/products)
public sealed class ReportProductSalesResponse
{
    public IReadOnlyList<ReportProductSalesResponseRow> Rows { get; set; } = default!;
}

public sealed class ReportProductSalesResponseRow
{
    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = default!;

    public decimal QuantitySold { get; set; }

    public decimal QuantityReturned { get; set; }

    public decimal NetQuantity { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }

    public decimal NetSales { get; set; }

    public decimal DiscountTotal { get; set; }

    // cost がある商品のみ
    public decimal? GrossProfit { get; set; }
}
