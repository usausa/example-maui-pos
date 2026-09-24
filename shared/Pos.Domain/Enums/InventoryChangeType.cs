namespace Pos.Domain.Enums;

// 在庫の変動。Receive は仕入先からの入荷 (+)、TransferOut / TransferIn は店舗間移動の出荷 (−) と受領 (+)
public enum InventoryChangeType
{
    Sale,
    Return,
    Void,
    PhysicalCount,
    Adjustment,
    Receive,
    TransferOut,
    TransferIn
}

public static class InventoryChangeTypeExtensions
{
    // 棚卸・調整 (人が数を入れて登録する変動)。ほかの種別は取引・入荷・移動の記録と一緒に作る
    public static bool IsManual(this InventoryChangeType type) => type is InventoryChangeType.PhysicalCount or InventoryChangeType.Adjustment;
}
