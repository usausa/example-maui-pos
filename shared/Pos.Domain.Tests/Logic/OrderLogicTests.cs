namespace Pos.Domain.Logic;

public sealed class OrderLogicTests
{
    private static readonly Guid StoreId = new("00000000-0000-0000-0001-000000000001");

    private static readonly Guid OtherStoreId = new("00000000-0000-0000-0001-000000000002");

    // 取り寄せは入荷待ち、取り置きは引き渡し待ちから始まる
    [Theory]
    [InlineData(OrderType.BackOrder, OrderStatus.Ordered)]
    [InlineData(OrderType.Hold, OrderStatus.Arrived)]
    public void InitialStatus(OrderType type, OrderStatus expected)
    {
        Assert.Equal(expected, OrderLogic.InitialStatus(type));
    }

    // 入荷は入荷待ちだけ、変更とキャンセルは未完了だけ
    [Theory]
    [InlineData(OrderStatus.Ordered, true, true, true)]
    [InlineData(OrderStatus.Arrived, false, true, true)]
    [InlineData(OrderStatus.Completed, false, false, false)]
    [InlineData(OrderStatus.Cancelled, false, false, false)]
    public void ValidateTransitions(OrderStatus status, bool canArrive, bool canUpdate, bool canCancel)
    {
        Assert.Equal(canArrive, OrderLogic.ValidateArrive(status) is null);
        Assert.Equal(canUpdate, OrderLogic.ValidateUpdate(status) is null);
        Assert.Equal(canCancel, OrderLogic.ValidateCancel(status) is null);
        Assert.Equal(canUpdate, status.IsOpen());
    }

    // 会計できるのは自店の引き渡し待ちの受注だけ
    [Fact]
    public void ValidateCheckout()
    {
        // Arrange
        var arrived = new OrderFact { Id = Guid.NewGuid(), StoreId = StoreId, Status = OrderStatus.Arrived };
        var ordered = new OrderFact { Id = Guid.NewGuid(), StoreId = StoreId, Status = OrderStatus.Ordered };

        // Act / Assert
        Assert.Null(OrderLogic.ValidateCheckout(arrived, StoreId));
        Assert.Equal(ErrorCode.OrderNotReady, OrderLogic.ValidateCheckout(ordered, StoreId)?.Code);
        Assert.Equal(ErrorCode.OrderNotFound, OrderLogic.ValidateCheckout(arrived, OtherStoreId)?.Code);
        Assert.Equal(ErrorCode.OrderNotFound, OrderLogic.ValidateCheckout(null, StoreId)?.Code);
    }

    // 明細の金額は単価 × 数量の切り捨て
    [Fact]
    public void LineAmount()
    {
        Assert.Equal(3333m, OrderLogic.LineAmount(1333.33m, 2.5m));
    }
}
