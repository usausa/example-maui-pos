namespace Pos.Server.Models.Entity;

[Name("InventoryTransfers")]
public sealed class InventoryTransferEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid FromStoreId { get; set; }

    // 出荷店ごとの連番 (登録時に採番)
    public int Seq { get; set; }

    public string TransferNo { get; set; } = default!;

    public Guid ToStoreId { get; set; }

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
}
