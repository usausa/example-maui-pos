namespace Pos.Domain.Logic;

public sealed class OrderLogicTests
{
    private static readonly Guid StoreId = new("00000000-0000-0000-0001-000000000001");

    private static readonly Guid OtherStoreId = new("00000000-0000-0000-0001-000000000002");

    private static readonly Guid TerminalId = new("00000000-0000-0000-0006-000000000001");

    private static readonly Guid OtherTerminalId = new("00000000-0000-0000-0006-000000000002");

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

    // 前受金があるうちはキャンセルできない
    [Fact]
    public void ValidateCancelWithDeposit()
    {
        Assert.Null(OrderLogic.ValidateCancel(OrderStatus.Ordered, 0m));
        Assert.Equal((ErrorCode.OrderDepositInvalid, RuleReason.DepositHeld), Pair(OrderLogic.ValidateCancel(OrderStatus.Arrived, 1000m)));
        Assert.Equal((ErrorCode.OrderStatusInvalid, RuleReason.OrderNotCancellable), Pair(OrderLogic.ValidateCancel(OrderStatus.Completed, 0m)));
    }

    // 前受金は未完了の受注に 1 つだけ、1 円以上で受注の金額まで、現金・カード・QR・電子マネーで受け取る
    [Theory]
    [InlineData(OrderStatus.Ordered, 0, 1000, PaymentKind.Cash, null)]
    [InlineData(OrderStatus.Arrived, 0, 5000, PaymentKind.EMoney, null)]
    [InlineData(OrderStatus.Completed, 0, 1000, PaymentKind.Cash, RuleReason.OrderNotEditable)]
    [InlineData(OrderStatus.Ordered, 1000, 1000, PaymentKind.Cash, RuleReason.DepositExists)]
    [InlineData(OrderStatus.Ordered, 0, 0, PaymentKind.Cash, RuleReason.DepositAmountInvalid)]
    [InlineData(OrderStatus.Ordered, 0, 5001, PaymentKind.Card, RuleReason.DepositAmountInvalid)]
    [InlineData(OrderStatus.Ordered, 0, 1000, PaymentKind.Points, RuleReason.DepositMethodInvalid)]
    [InlineData(OrderStatus.Ordered, 0, 1000, PaymentKind.Deposit, RuleReason.DepositMethodInvalid)]
    public void ValidateDeposit(OrderStatus status, int balance, int amount, PaymentKind kind, RuleReason? expected)
    {
        Assert.Equal(expected, OrderLogic.ValidateDeposit(status, balance, 5000m, amount, kind)?.Reason);
    }

    // 返すのは未完了の受注の前受金だけ
    [Fact]
    public void ValidateDepositRefund()
    {
        Assert.Null(OrderLogic.ValidateDepositRefund(OrderStatus.Arrived, 1000m));
        Assert.Equal((ErrorCode.OrderDepositInvalid, RuleReason.DepositNotFound), Pair(OrderLogic.ValidateDepositRefund(OrderStatus.Ordered, 0m)));
        Assert.Equal((ErrorCode.OrderStatusInvalid, RuleReason.OrderNotEditable), Pair(OrderLogic.ValidateDepositRefund(OrderStatus.Cancelled, 0m)));
    }

    // 前受金は開設中のシフトで、その端末から自店の受注に。担当は自店の有効なスタッフ
    [Fact]
    public void ValidateDepositPlace()
    {
        // Arrange
        var shift = new ShiftFact { Id = Guid.NewGuid(), Status = ShiftStatus.Open, TerminalId = TerminalId, StoreId = StoreId };
        var closed = new ShiftFact { Id = Guid.NewGuid(), Status = ShiftStatus.Closed, TerminalId = TerminalId, StoreId = StoreId };
        var staff = new StaffFact { Id = Guid.NewGuid(), Role = StaffRole.Cashier, StoreId = StoreId };
        var otherStaff = new StaffFact { Id = Guid.NewGuid(), Role = StaffRole.Cashier, StoreId = OtherStoreId };

        // Act / Assert
        Assert.Null(OrderLogic.ValidateDepositPlace(shift, TerminalId, StoreId, staff));
        Assert.Equal(ErrorCode.ShiftNotFound, OrderLogic.ValidateDepositPlace(null, TerminalId, StoreId, staff)?.Code);
        Assert.Equal(ErrorCode.ShiftClosed, OrderLogic.ValidateDepositPlace(closed, TerminalId, StoreId, staff)?.Code);
        Assert.Equal(ErrorCode.ShiftTerminalMismatch, OrderLogic.ValidateDepositPlace(shift, OtherTerminalId, StoreId, staff)?.Code);
        Assert.Equal(ErrorCode.OrderNotFound, OrderLogic.ValidateDepositPlace(shift, TerminalId, OtherStoreId, staff)?.Code);
        Assert.Equal(ErrorCode.StaffInvalid, OrderLogic.ValidateDepositPlace(shift, TerminalId, StoreId, otherStaff)?.Code);
    }

    // 会計で充てる前受金は未完了の受注だけ (完了は会計で充てた、キャンセルは返した)
    [Theory]
    [InlineData(OrderStatus.Ordered, 1000)]
    [InlineData(OrderStatus.Arrived, 1000)]
    [InlineData(OrderStatus.Completed, 0)]
    [InlineData(OrderStatus.Cancelled, 0)]
    public void DepositBalance(OrderStatus status, int expected)
    {
        Assert.Equal(expected, OrderLogic.DepositBalance(status, 1000m));
    }

    // 明細の金額は単価 × 数量の切り捨て
    [Fact]
    public void LineAmount()
    {
        Assert.Equal(3333m, OrderLogic.LineAmount(1333.33m, 2.5m));
    }

    private static (ErrorCode, RuleReason)? Pair(RuleError? error) =>
        error is null ? null : (error.Code, error.Reason);
}
