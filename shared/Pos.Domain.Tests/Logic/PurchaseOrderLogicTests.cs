namespace Pos.Domain.Logic;

// 発注の状態遷移
public sealed class PurchaseOrderLogicTests
{
    // 変更と発注は下書きのとき、キャンセルは下書きと発注済み (未完了) のときだけ
    [Theory]
    [InlineData(PurchaseOrderStatus.Draft, true, true)]
    [InlineData(PurchaseOrderStatus.Ordered, false, true)]
    [InlineData(PurchaseOrderStatus.Received, false, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, false, false)]
    public void ValidateTransitions(PurchaseOrderStatus status, bool editable, bool cancellable)
    {
        // Act
        var update = PurchaseOrderLogic.ValidateUpdate(status);
        var order = PurchaseOrderLogic.ValidateOrder(status);
        var cancel = PurchaseOrderLogic.ValidateCancel(status);

        // Assert
        Assert.Equal(editable, update is null);
        Assert.Equal(editable, order is null);
        Assert.Equal(cancellable, cancel is null);
        Assert.Equal(cancellable, status.IsOpen());
        if (!editable)
        {
            Assert.Equal((ErrorCode.PurchaseOrderStatusInvalid, RuleReason.PurchaseOrderNotDraft), (update!.Code, update.Reason));
        }

        if (!cancellable)
        {
            Assert.Equal((ErrorCode.PurchaseOrderStatusInvalid, RuleReason.PurchaseOrderNotOpen), (cancel!.Code, cancel.Reason));
        }
    }
}
