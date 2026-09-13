namespace Pos.Terminal.State;

// 棚卸・在庫調整 (T-70) の未送信リスト。スキャン画面との行き来で失わないよう画面の外に置く
public sealed class StockState
{
    public bool IsAdjustment { get; set; }

    public Collection<StockChange> Changes { get; } = [];
}

public sealed class StockChange
{
    public Guid Id { get; set; }

    public ProductResponse Product { get; set; } = default!;

    public InventoryChangeType Type { get; set; }

    // 棚卸は実数、調整は増減
    public decimal Quantity { get; set; }

    public decimal Before { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }
}
