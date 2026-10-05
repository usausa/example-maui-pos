namespace Pos.Contract.Inventory;

using Pos.Contract;

// 現在庫
public sealed class InventoryLevelListResponseItem
{
    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    // 負も許容し、要確認として扱う
    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class InventoryLevelListResponse : ListResponse<InventoryLevelListResponseItem>;

// 商品の全店舗在庫 (GET /inventory/{productId})
