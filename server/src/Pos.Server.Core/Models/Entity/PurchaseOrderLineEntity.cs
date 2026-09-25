namespace Pos.Server.Models.Entity;

[Name("PurchaseOrderLines")]
public sealed class PurchaseOrderLineEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    // 入荷予定の明細と同じ番号 (受領した数を引く)
    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public decimal Quantity { get; set; }

    // 仕入単価
    public decimal? Cost { get; set; }
}
