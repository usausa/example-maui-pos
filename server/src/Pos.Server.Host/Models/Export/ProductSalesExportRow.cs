namespace Pos.Server.Host.Models.Export;

using CsvHelper;

// 商品別売上 CSV の 1 行 (GET /reports/sales/products/csv)
[CsvRecord]
public sealed partial class ProductSalesExportRow
{
    [CsvColumn("商品コード")]
    public string ProductCode { get; set; } = default!;

    [CsvColumn("商品名")]
    public string ProductName { get; set; } = default!;

    [CsvColumn("部門")]
    public string CategoryName { get; set; } = default!;

    [CsvColumn("販売数量")]
    public decimal QuantitySold { get; set; }

    [CsvColumn("返品数量")]
    public decimal QuantityReturned { get; set; }

    [CsvColumn("純数量")]
    public decimal NetQuantity { get; set; }

    [CsvColumn("売上")]
    public decimal SalesTotal { get; set; }

    [CsvColumn("返品")]
    public decimal ReturnsTotal { get; set; }

    [CsvColumn("純売上")]
    public decimal NetSales { get; set; }

    [CsvColumn("値引")]
    public decimal DiscountTotal { get; set; }

    [CsvColumn("粗利")]
    public decimal? GrossProfit { get; set; }
}
