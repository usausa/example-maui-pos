namespace Pos.Server.Host.Models.Export;

using CsvHelper.Configuration.Attributes;

// 売上集計 CSV の 1 行 (GET /reports/sales/summary/csv)
public sealed class SalesSummaryExportRow
{
    [Name("キー")]
    public string Key { get; set; } = default!;

    [Name("名称")]
    public string Label { get; set; } = default!;

    [Name("販売件数")]
    public int TransactionCount { get; set; }

    [Name("返品件数")]
    public int ReturnCount { get; set; }

    [Name("客数")]
    public int CustomerCount { get; set; }

    [Name("売上")]
    public decimal SalesTotal { get; set; }

    [Name("返品")]
    public decimal ReturnsTotal { get; set; }

    [Name("純売上")]
    public decimal NetSales { get; set; }

    [Name("値引")]
    public decimal DiscountTotal { get; set; }

    [Name("税")]
    public decimal TaxTotal { get; set; }

    [Name("ポイント付与")]
    public int PointsEarned { get; set; }

    [Name("ポイント利用")]
    public int PointsRedeemed { get; set; }

    [Name("課税対象")]
    public decimal? TaxableAmount { get; set; }

    [Name("税額")]
    public decimal? TaxAmount { get; set; }
}
