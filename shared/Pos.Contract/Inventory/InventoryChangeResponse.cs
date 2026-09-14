namespace Pos.Contract.Inventory;

using Pos.Contract;

// 在庫変動履歴 (api-design §3.14)
public sealed class InventoryChangeResponseItem
{
    // 端末採番 (棚卸・調整) / サーバ採番 (取引由来)
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public InventoryChangeType Type { get; set; }

    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }

    // 取引由来なら "Transaction"
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public Guid? ReferenceLineId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class InventoryChangeResponse : ListResponse<InventoryChangeResponseItem>;
