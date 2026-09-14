namespace Pos.Terminal.Models;

// 棚卸・在庫調整の 1 件
public sealed class StockChange
{
    public Guid Id { get; set; }

    public ProductResponseItem Product { get; set; } = default!;

    public InventoryChangeType Type { get; set; }

    // 棚卸は実数、調整は増減
    public decimal Quantity { get; set; }

    public decimal Before { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }
}
