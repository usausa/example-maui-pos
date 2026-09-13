namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

// 棚卸・調整の登録 (S-43)
public sealed class InventoryChangeForm
{
    public Guid? StoreId { get; set; }

    public ProductEntity? Product { get; set; }

    public InventoryChangeType Type { get; set; } = InventoryChangeType.PhysicalCount;

    // PhysicalCount は実数、Adjustment は増減
    public decimal Quantity { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }

    public Guid? StaffId { get; set; }
}
