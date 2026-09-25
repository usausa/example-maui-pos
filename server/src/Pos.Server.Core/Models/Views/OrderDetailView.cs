namespace Pos.Server.Models.Views;

using Pos.Domain.Logic;
using Pos.Server.Models.Entity;

// 受注と明細、前受金の記録
public sealed class OrderDetailView
{
    public required OrderEntity Order { get; init; }

    public required IReadOnlyList<OrderLineEntity> Lines { get; init; }

    public IReadOnlyList<OrderDepositEntity> Deposits { get; init; } = [];

    // 受け取った額 − 返した額 (完了した受注は会計で充てた額)
    public decimal DepositNet => NetOf(Deposits);

    // 会計で充てる前受金 (完了・キャンセルした受注は 0)
    public decimal DepositBalance => OrderLogic.DepositBalance(Order.Status, DepositNet);

    public static decimal DepositBalanceOf(OrderStatus status, IEnumerable<OrderDepositEntity> deposits) =>
        OrderLogic.DepositBalance(status, NetOf(deposits));

    private static decimal NetOf(IEnumerable<OrderDepositEntity> deposits) =>
        deposits.Sum(static x => x.Type == OrderDepositType.Receive ? x.Amount : -x.Amount);
}
