namespace Pos.Domain.Enums;

// 発注。下書き (Draft) → 発注 (Ordered。入荷予定を作る) → 入荷済み (Received。入荷予定の受領)。キャンセルは入荷の前だけ
public enum PurchaseOrderStatus
{
    Draft,
    Ordered,
    Received,
    Cancelled
}

public static class PurchaseOrderStatusExtensions
{
    // 未完了 (下書き・発注済み)。キャンセルができる
    public static bool IsOpen(this PurchaseOrderStatus status) => status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered;
}
