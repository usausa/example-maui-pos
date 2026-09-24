namespace Pos.Server.Models.Entity;

[Name("InventoryReceipts")]
public sealed class InventoryReceiptEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid SupplierId { get; set; }

    // 仕入先の納品書番号
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
}
