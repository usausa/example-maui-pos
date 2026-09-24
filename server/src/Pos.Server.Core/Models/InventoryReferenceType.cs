namespace Pos.Server.Models;

// 在庫変動の参照元 (InventoryChanges.ReferenceType)。ReferenceId はその資源の Id、ReferenceLineId は明細の Id
public static class InventoryReferenceType
{
    public const string Transaction = "Transaction";

    public const string InventoryReceipt = "InventoryReceipt";

    public const string InventoryTransfer = "InventoryTransfer";
}
