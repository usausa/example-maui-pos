namespace Pos.Contract.InventoryReceipts;

using Pos.Contract;

// 入荷 (仕入先からの入荷予定と受領)
public sealed class InventoryReceiptResponseItem
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = default!;

    // 発注から作った入荷予定はその発注 (ほかは null)
    public Guid? PurchaseOrderId { get; set; }

    public string? PurchaseOrderNo { get; set; }

    public string? SlipNo { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public InventoryReceiptStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public Guid? ReceivedByStaffId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }

    public IReadOnlyList<InventoryReceiptResponseLine> Lines { get; set; } = default!;
}

public sealed class InventoryReceiptResponseLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    // 予定の数
    public decimal Quantity { get; set; }

    // 受領した数 (受領まで null)
    public decimal? ReceivedQuantity { get; set; }

    public decimal? Cost { get; set; }
}

public sealed class InventoryReceiptResponse : ListResponse<InventoryReceiptResponseItem>;
