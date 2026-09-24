namespace Pos.Contract.InventoryTransfers;

using Pos.Contract;

// 店舗間移動
public sealed class InventoryTransferResponseItem
{
    public Guid Id { get; set; }

    // {出荷店コード}-T-{連番:000000}
    public string TransferNo { get; set; } = default!;

    public Guid FromStoreId { get; set; }

    public string FromStoreName { get; set; } = default!;

    public Guid ToStoreId { get; set; }

    public string ToStoreName { get; set; } = default!;

    public InventoryTransferStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTime? ShippedAt { get; set; }

    public Guid? ShippedByStaffId { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public Guid? ReceivedByStaffId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }

    public IReadOnlyList<InventoryTransferResponseLine> Lines { get; set; } = default!;
}

public sealed class InventoryTransferResponseLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    // 依頼・出荷の数
    public decimal Quantity { get; set; }

    // 受領した数 (受領まで null)
    public decimal? ReceivedQuantity { get; set; }
}

public sealed class InventoryTransferResponse : ListResponse<InventoryTransferResponseItem>;
