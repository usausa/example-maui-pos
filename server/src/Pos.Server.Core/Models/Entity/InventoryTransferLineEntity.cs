namespace Pos.Server.Models.Entity;

[Name("InventoryTransferLines")]
public sealed class InventoryTransferLineEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransferId { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    // 依頼・出荷の数
    public decimal Quantity { get; set; }

    // 受領した数 (受領まで null)
    public decimal? ReceivedQuantity { get; set; }
}
