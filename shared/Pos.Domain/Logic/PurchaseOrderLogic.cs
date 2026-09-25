namespace Pos.Domain.Logic;

// 発注の業務ルール (状態遷移)。在庫は発注では動かさず、発注で作った入荷予定の受領で入る
public static class PurchaseOrderLogic
{
    // 変更と発注は下書きのときだけ (発注した後の内容は入荷予定に写してある)
    public static RuleError? ValidateUpdate(PurchaseOrderStatus status) =>
        status == PurchaseOrderStatus.Draft ? null : new RuleError(ErrorCode.PurchaseOrderStatusInvalid, RuleReason.PurchaseOrderNotDraft);

    public static RuleError? ValidateOrder(PurchaseOrderStatus status) =>
        ValidateUpdate(status);

    // キャンセルは入荷の前 (下書きか発注済み) だけ
    public static RuleError? ValidateCancel(PurchaseOrderStatus status) =>
        status.IsOpen() ? null : new RuleError(ErrorCode.PurchaseOrderStatusInvalid, RuleReason.PurchaseOrderNotOpen);
}
