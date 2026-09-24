namespace Pos.Server.Models.Entity;

// 商品名・単価は受注時点のスナップショット
[Name("OrderLines")]
public sealed class OrderLineEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
