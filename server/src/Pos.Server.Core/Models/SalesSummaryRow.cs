namespace Pos.Server.Models;

// 売上集計の 1 行 (api-design §3.15)。返品は負として合算し、取消済みは除く
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

public sealed record ProductSalesRow(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid CategoryId,
    string CategoryName,
    decimal QuantitySold,
    decimal QuantityReturned,
    decimal NetQuantity,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal NetSales,
    decimal DiscountTotal,
    decimal? GrossProfit);

public sealed record ProductInventoryLevel(
    Guid StoreId,
    string StoreName,
    decimal Quantity,
    DateTime UpdatedAt);

// 取引ベースの売上集計のグループ (時間帯・支払方法・税率・部門は専用クエリ)
public enum SalesSummaryGroup
{
    Day,
    Terminal,
    Staff
}
