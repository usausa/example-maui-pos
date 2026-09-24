namespace Pos.Domain.Logic;

// 入荷と店舗間移動の業務ルール (状態遷移)。在庫は入荷の受領で増え、移動は出荷で出荷店から減り、受領で入荷店に増える
public static class InventoryMovementLogic
{
    // 入荷の受領とキャンセルは入荷予定のときだけ
    public static RuleError? ValidateReceiptReceive(InventoryReceiptStatus status) =>
        status == InventoryReceiptStatus.Draft ? null : new RuleError(ErrorCode.InventoryReceiptStatusInvalid, RuleReason.InventoryReceiptNotDraft);

    public static RuleError? ValidateReceiptCancel(InventoryReceiptStatus status) =>
        ValidateReceiptReceive(status);

    // 出荷とキャンセルは依頼のときだけ (出荷した後は在庫が動いているので取り消さない)
    public static RuleError? ValidateTransferShip(InventoryTransferStatus status) =>
        status == InventoryTransferStatus.Requested ? null : new RuleError(ErrorCode.InventoryTransferStatusInvalid, RuleReason.InventoryTransferNotRequested);

    public static RuleError? ValidateTransferCancel(InventoryTransferStatus status) =>
        ValidateTransferShip(status);

    public static RuleError? ValidateTransferReceive(InventoryTransferStatus status) =>
        status == InventoryTransferStatus.Shipped ? null : new RuleError(ErrorCode.InventoryTransferStatusInvalid, RuleReason.InventoryTransferNotShipped);
}
