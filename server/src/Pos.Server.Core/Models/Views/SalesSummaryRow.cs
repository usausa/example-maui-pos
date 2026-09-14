namespace Pos.Server.Models.Views;

// 売上集計の 1 行。返品は負として合算し、取消済みは除く。TaxableAmount / TaxAmount は税率別のときだけ
public sealed record SalesSummaryRow(
    string GroupKey,
    string GroupLabel,
    int TransactionCount,
    int ReturnCount,
    int CustomerCount,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal NetSales,
    decimal DiscountTotal,
    decimal TaxTotal,
    int PointsEarned,
    int PointsRedeemed,
    decimal? TaxableAmount = null,
    decimal? TaxAmount = null);
