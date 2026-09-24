namespace Pos.Contract.InventoryTransfers;

// 出荷 (依頼の数で出荷店の在庫を減らす)
public sealed class InventoryTransferShipRequest
{
    public Guid? StaffId { get; set; }

    // 省略するとサーバの受付時刻
    public DateTime? ShippedAt { get; set; }
}
