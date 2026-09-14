namespace Pos.Server.Models.Views;

// 商品の店舗別在庫 (他店在庫照会)
public sealed record ProductInventoryLevel(
    Guid StoreId,
    string StoreName,
    decimal Quantity,
    DateTime UpdatedAt);
