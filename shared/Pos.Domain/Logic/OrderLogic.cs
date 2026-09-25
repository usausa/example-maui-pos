namespace Pos.Domain.Logic;

// 受注 (取り寄せ・取り置き) の業務ルール。受注は会計前の約束で、在庫は会計時に減らす (受注では引き当てない)
public static class OrderLogic
{
    // 取り置きは店頭の在庫を確保するので、登録した時点で引き渡し待ち
    public static OrderStatus InitialStatus(OrderType type) =>
        type == OrderType.Hold ? OrderStatus.Arrived : OrderStatus.Ordered;

    // 明細の金額 (販売の明細と同じく単価 × 数量を切り捨て)
    public static decimal LineAmount(decimal unitPrice, decimal quantity) => Math.Floor(unitPrice * quantity);

    // 連絡先・明細・希望日・備考の変更
    public static RuleError? ValidateUpdate(OrderStatus status) =>
        status.IsOpen() ? null : new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotEditable);

    public static RuleError? ValidateArrive(OrderStatus status) =>
        status == OrderStatus.Ordered ? null : new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotOrdered);

    public static RuleError? ValidateCancel(OrderStatus status) =>
        status.IsOpen() ? null : new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotCancellable);

    // 前受金があるときは、返してからキャンセルする
    public static RuleError? ValidateCancel(OrderStatus status, decimal depositBalance) =>
        ValidateCancel(status) ?? (depositBalance > 0 ? new RuleError(ErrorCode.OrderDepositInvalid, RuleReason.DepositHeld) : null);

    // 前受金の受取は未完了の受注で、前受金がないときだけ (1 つの受注に 1 つ)。1 円以上、受注の金額まで
    public static RuleError? ValidateDeposit(OrderStatus status, decimal depositBalance, decimal total, decimal amount, PaymentKind kind)
    {
        if (!status.IsOpen())
        {
            return new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotEditable);
        }

        if (depositBalance > 0)
        {
            return new RuleError(ErrorCode.OrderDepositInvalid, RuleReason.DepositExists);
        }

        if ((amount <= 0) || (amount > total))
        {
            return new RuleError(ErrorCode.OrderDepositInvalid, RuleReason.DepositAmountInvalid);
        }

        return kind.CanReceiveDeposit() ? null : new RuleError(ErrorCode.OrderDepositInvalid, RuleReason.DepositMethodInvalid);
    }

    // 前受金は取引と同じく、開設中のシフトでその端末から自店の受注に受け取る (返す)。担当は自店 (または本部) の有効なスタッフ
    public static RuleError? ValidateDepositPlace(ShiftFact? shift, Guid terminalId, Guid storeId, StaffFact? staff)
    {
        if (TransactionLogic.ValidateShift(shift, terminalId) is { } error)
        {
            return error;
        }

        return shift!.StoreId == storeId ? StaffLogic.ValidateStaff(staff, storeId) : new RuleError(ErrorCode.OrderNotFound, RuleReason.OrderNotFound);
    }

    // 前受金を返すのは未完了の受注で、前受金があるときだけ (全額を、受け取った方法で返す)
    public static RuleError? ValidateDepositRefund(OrderStatus status, decimal depositBalance)
    {
        if (!status.IsOpen())
        {
            return new RuleError(ErrorCode.OrderStatusInvalid, RuleReason.OrderNotEditable);
        }

        return depositBalance > 0 ? null : new RuleError(ErrorCode.OrderDepositInvalid, RuleReason.DepositNotFound);
    }

    // 会計で充てる前受金 (net = 受け取った額 − 返した額)。完了した受注は会計で充てたので 0
    public static decimal DepositBalance(OrderStatus status, decimal net) =>
        status.IsOpen() ? net : 0m;

    // 会計できるのは自店の引き渡し待ちの受注だけ (order = null は見つからない)
    public static RuleError? ValidateCheckout(OrderFact? order, Guid storeId)
    {
        if ((order is null) || (order.StoreId != storeId))
        {
            return new RuleError(ErrorCode.OrderNotFound, RuleReason.OrderNotFound);
        }

        return order.Status == OrderStatus.Arrived ? null : new RuleError(ErrorCode.OrderNotReady, RuleReason.OrderNotReady);
    }
}
