namespace Pos.Server.Models.Parameters;

// 棚卸 (絶対数量) / 調整 (増減) の登録内容。Id は呼び出し側が採番する (同じ Id は重複として扱う)
public sealed class InventoryChangeParameter
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public InventoryChangeType Type { get; set; }

    // PhysicalCount は絶対数量、Adjustment は符号付き増減
    public decimal Quantity { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime OccurredAt { get; set; }
}
