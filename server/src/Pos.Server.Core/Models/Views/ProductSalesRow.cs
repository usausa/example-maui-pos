namespace Pos.Server.Models.Views;

// 商品別売上の 1 行 (GrossProfit は原価のある商品のみ)
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
