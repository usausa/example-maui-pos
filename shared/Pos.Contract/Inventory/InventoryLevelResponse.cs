namespace Pos.Contract.Inventory;

using Pos.Contract;

// 現在庫 (api-design §3.14)
public sealed class InventoryLevelResponseItem
{
    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    // 負も許容し、要確認として扱う
    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class InventoryLevelResponse : ListResponse<InventoryLevelResponseItem>;

// 商品の全店舗在庫 (GET /inventory/{productId})
public sealed class ProductInventoryResponse
{
    public Guid ProductId { get; set; }

    public IReadOnlyList<ProductInventoryResponseLevel> Levels { get; set; } = default!;
}

public sealed class ProductInventoryResponseLevel
{
    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}
