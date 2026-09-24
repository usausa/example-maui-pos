namespace Pos.Server.Models.Views;

// 状態ごとの受注の件数 (ダッシュボードの入荷待ち・引き渡し待ち)
public sealed record OrderStatusCountView(OrderStatus Status, int Count);
