namespace Pos.Server.Models.Entity;

[Name("InventoryReceiptLines")]
public sealed class InventoryReceiptLineEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    // 予定の数
    public decimal Quantity { get; set; }

    // 受領した数 (受領まで null)
    public decimal? ReceivedQuantity { get; set; }

    // 仕入単価
    public decimal? Cost { get; set; }
}
