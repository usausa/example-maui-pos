namespace Pos.Contract.Inventory;

// 棚卸・調整の一括登録の結果 (要素ごと)
public sealed class InventoryChangeResultResponse
{
    public IReadOnlyList<InventoryChangeResultResponseResult> Results { get; set; } = default!;
}

public sealed class InventoryChangeResultResponseResult
{
    public Guid Id { get; set; }

    public InventoryChangeResultStatus Status { get; set; }

    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }
}
