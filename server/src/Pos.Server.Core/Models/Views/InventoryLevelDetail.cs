namespace Pos.Server.Models.Views;

// 現在庫の一覧 (管理画面用。店舗名・商品名付き)
public sealed record InventoryLevelDetail(
    Guid StoreId,
    string StoreName,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid CategoryId,
    string CategoryName,
    decimal Quantity,
    DateTime UpdatedAt);
