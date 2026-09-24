namespace Pos.Domain.Enums;

// 受注の状態。入荷待ち (Ordered) → 引き渡し待ち (Arrived) → 会計で完了 (Completed)。完了前ならキャンセル (Cancelled) できる
public enum OrderStatus
{
    Ordered,
    Arrived,
    Completed,
    Cancelled
}

public static class OrderStatusExtensions
{
    // 未完了 (入荷待ち・引き渡し待ち)。変更とキャンセルができる
    public static bool IsOpen(this OrderStatus status) => status is OrderStatus.Ordered or OrderStatus.Arrived;
}
