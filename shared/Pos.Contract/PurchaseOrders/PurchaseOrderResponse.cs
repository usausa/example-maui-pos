namespace Pos.Contract.PurchaseOrders;

using Pos.Contract;

// 発注 (仕入先への注文と、発注で作った入荷予定)
public sealed class PurchaseOrderResponseItem
{
    public Guid Id { get; set; }

    public string PurchaseOrderNo { get; set; } = default!;

    public Guid StoreId { get; set; }

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = default!;

    public PurchaseOrderStatus Status { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public DateTime? OrderedAt { get; set; }

    // 発注した管理画面のアカウント名
    public string? OrderedBy { get; set; }

    // 発注で作った入荷予定
    public Guid? ReceiptId { get; set; }

    public DateTime? CancelledAt { get; set; }

    // 数量 × 仕入単価の合計 (単価のない明細は含めない)
    public decimal TotalCost { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }

    public IReadOnlyList<PurchaseOrderResponseLine> Lines { get; set; } = default!;
}

public sealed class PurchaseOrderResponseLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public decimal? Cost { get; set; }

    // 入荷予定で受領した数 (受領まで null)
    public decimal? ReceivedQuantity { get; set; }
}

public sealed class PurchaseOrderResponse : ListResponse<PurchaseOrderResponseItem>;
