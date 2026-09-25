namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 入荷と明細 (仕入先の名前付き)
public sealed class InventoryReceiptDetailView
{
    public required InventoryReceiptEntity Receipt { get; init; }

    public required string SupplierName { get; init; }

    public required IReadOnlyList<InventoryReceiptLineEntity> Lines { get; init; }

    // 発注から作った入荷予定はその発注
    public PurchaseOrderEntity? PurchaseOrder { get; init; }
}
