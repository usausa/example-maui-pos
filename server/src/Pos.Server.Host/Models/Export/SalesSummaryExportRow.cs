namespace Pos.Server.Host.Models.Export;

using CsvHelper;

// 売上集計 CSV の 1 行 (GET /reports/sales/summary/csv)
[CsvRecord]
public sealed partial class SalesSummaryExportRow
{
    [CsvColumn("キー")]
    public string Key { get; set; } = default!;

    [CsvColumn("名称")]
    public string Label { get; set; } = default!;

    [CsvColumn("販売件数")]
    public int TransactionCount { get; set; }

    [CsvColumn("返品件数")]
    public int ReturnCount { get; set; }

    [CsvColumn("客数")]
    public int CustomerCount { get; set; }

    [CsvColumn("売上")]
    public decimal SalesTotal { get; set; }

    [CsvColumn("返品")]
    public decimal ReturnsTotal { get; set; }

    [CsvColumn("純売上")]
    public decimal NetSales { get; set; }

    [CsvColumn("値引")]
    public decimal DiscountTotal { get; set; }

    [CsvColumn("税")]
    public decimal TaxTotal { get; set; }

    [CsvColumn("ポイント付与")]
    public int PointsEarned { get; set; }

    [CsvColumn("ポイント利用")]
    public int PointsRedeemed { get; set; }

    [CsvColumn("課税対象")]
    public decimal? TaxableAmount { get; set; }

    [CsvColumn("税額")]
    public decimal? TaxAmount { get; set; }
}
