namespace Pos.Terminal.Modules.Inventory;

// 棚卸・在庫調整の未送信リスト。スキャン画面との行き来で失わないようナビゲーションのパラメータで渡す
public sealed class StockContext
{
    public bool IsAdjustment { get; set; }

    public Collection<StockChange> Changes { get; } = [];
}
