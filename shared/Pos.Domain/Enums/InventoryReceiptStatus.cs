namespace Pos.Domain.Enums;

// 入荷。入荷予定 (Draft) を受領すると在庫に入る。キャンセルは受領の前だけ
public enum InventoryReceiptStatus
{
    Draft,
    Received,
    Cancelled
}
