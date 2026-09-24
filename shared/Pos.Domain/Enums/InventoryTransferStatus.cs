namespace Pos.Domain.Enums;

// 店舗間移動。依頼 (Requested) → 出荷 (Shipped。出荷店の在庫を減らす) → 受領 (Received。入荷店の在庫を増やす)。キャンセルは出荷の前だけ
public enum InventoryTransferStatus
{
    Requested,
    Shipped,
    Received,
    Cancelled
}
