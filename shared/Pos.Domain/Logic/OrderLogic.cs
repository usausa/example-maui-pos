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
