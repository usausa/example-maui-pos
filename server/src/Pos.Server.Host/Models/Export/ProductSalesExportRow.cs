namespace Pos.Server.Host.Models.Export;

using CsvHelper.Configuration.Attributes;

using Pos.Server.Models;

// 商品別売上 CSV の 1 行 (GET /reports/sales/products/csv)
public sealed class ProductSalesExportRow
{
    [Name("商品コード")]
    public string ProductCode { get; set; } = default!;

    [Name("商品名")]
    public string ProductName { get; set; } = default!;

    [Name("部門")]
    public string CategoryName { get; set; } = default!;

    [Name("販売数量")]
    public decimal QuantitySold { get; set; }

    [Name("返品数量")]
    public decimal QuantityReturned { get; set; }

    [Name("純数量")]
    public decimal NetQuantity { get; set; }

    [Name("売上")]
    public decimal SalesTotal { get; set; }

    [Name("返品")]
    public decimal ReturnsTotal { get; set; }

    [Name("純売上")]
    public decimal NetSales { get; set; }

    [Name("値引")]
    public decimal DiscountTotal { get; set; }

    [Name("粗利")]
    public decimal? GrossProfit { get; set; }

    public static ProductSalesExportRow From(ProductSalesRow row) => new()
    {
        ProductCode = row.ProductCode,
        ProductName = row.ProductName,
        CategoryName = row.CategoryName,
        QuantitySold = row.QuantitySold,
        QuantityReturned = row.QuantityReturned,
        NetQuantity = row.NetQuantity,
        SalesTotal = row.SalesTotal,
        ReturnsTotal = row.ReturnsTotal,
        NetSales = row.NetSales,
        DiscountTotal = row.DiscountTotal,
        GrossProfit = row.GrossProfit
    };
}
